using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Adapters;

namespace Vetly.IntegrationTests;

/// <summary>
/// Storage de objetos e midias (§2.6, RN-090).
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class MidiaStorageTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public MidiaStorageTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    private static StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

    private static StorageAdapterLocal CriarStorage(out string diretorio)
    {
        diretorio = Path.Combine(Path.GetTempPath(), $"vetly-teste-{Guid.NewGuid():N}");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Diretorio"] = diretorio,
            ["Storage:BaseUrl"] = "/api/storage",
            ["Storage:Segredo"] = "segredo-de-teste-com-tamanho-suficiente"
        }).Build();

        return new StorageAdapterLocal(config, NullLogger<StorageAdapterLocal>.Instance);
    }

    private async Task<string> TokenDeResponsavelAsync()
    {
        var email = $"midia-{Guid.NewGuid():N}@exemplo.com";

        var registro = await _client.PostAsync("/api/auth/registro/tutor", Corpo(
            $$"""{"nome":"Ana","email":"{{email}}","telefone":"11999998888","senha":"senha-forte-123"}"""));

        var sessao = await registro.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = sessao.GetProperty("token").GetString()!;
        var tutorId = sessao.GetProperty("tutorId").GetGuid();

        var consentir = new HttpRequestMessage(HttpMethod.Put, $"/api/tutores/{tutorId}/consentimentos")
        {
            Content = Corpo("""{"consentimentos":[{"finalidade":"Atendimento","concedido":true}]}""")
        };
        consentir.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(consentir);

        return token;
    }

    // ── Assinatura das URLs ──────────────────────────────────────────────────

    [Fact]
    public async Task UrlDeUpload_TrazAssinaturaEExpiracao()
    {
        var storage = CriarStorage(out _);

        var url = await storage.GerarUrlDeUploadAsync("audioconsulta/2026/09/abc", "audio/webm", TimeSpan.FromMinutes(15));

        Assert.Contains("operacao=upload", url.Url);
        Assert.Contains("assinatura=", url.Url);
        Assert.True(url.ExpiraEm > DateTime.UtcNow);
    }

    [Fact]
    public async Task Assinatura_DeOutraChave_NaoConfere()
    {
        var storage = CriarStorage(out _);
        var url = await storage.GerarUrlDeUploadAsync("chave-a", "audio/webm", TimeSpan.FromMinutes(15));

        var assinatura = ExtrairAssinatura(url.Url);
        var expiraEm = ExtrairExpiracao(url.Url);

        // Sem isso, quem descobrisse o padrao da chave leria audio de consulta alheia
        Assert.True(storage.AssinaturaConfere("chave-a", "upload", expiraEm, assinatura));
        Assert.False(storage.AssinaturaConfere("chave-b", "upload", expiraEm, assinatura));
    }

    [Fact]
    public async Task Assinatura_DeOutraOperacao_NaoConfere()
    {
        var storage = CriarStorage(out _);
        var url = await storage.GerarUrlDeUploadAsync("chave-a", "audio/webm", TimeSpan.FromMinutes(15));

        // URL de upload nao serve para leitura
        Assert.False(storage.AssinaturaConfere(
            "chave-a", "leitura", ExtrairExpiracao(url.Url), ExtrairAssinatura(url.Url)));
    }

    [Fact]
    public async Task Assinatura_Expirada_NaoConfere()
    {
        var storage = CriarStorage(out _);
        var url = await storage.GerarUrlDeLeituraAsync("chave-a", TimeSpan.FromSeconds(-1));

        Assert.False(storage.AssinaturaConfere(
            "chave-a", "leitura", ExtrairExpiracao(url.Url), ExtrairAssinatura(url.Url)));
    }

    [Fact]
    public async Task Chave_QueTentaEscaparDoDiretorio_ERecusada()
    {
        var storage = CriarStorage(out _);

        // Sem essa trava, uma chave com ".." leria arquivo de fora do storage
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => storage.ExisteAsync("../../etc/senhas"));
    }

    // ── Ciclo do arquivo ─────────────────────────────────────────────────────

    [Fact]
    public async Task GravarEAbrir_PreservaOConteudo()
    {
        var storage = CriarStorage(out var diretorio);

        try
        {
            var conteudo = "bytes-de-audio-simulados"u8.ToArray();
            await storage.GravarAsync("audioconsulta/2026/09/abc", new MemoryStream(conteudo));

            Assert.True(await storage.ExisteAsync("audioconsulta/2026/09/abc"));
            Assert.Equal(conteudo.Length, await storage.ObterTamanhoAsync("audioconsulta/2026/09/abc"));

            using var leitura = storage.Abrir("audioconsulta/2026/09/abc");
            using var memoria = new MemoryStream();
            await leitura.CopyToAsync(memoria);

            Assert.Equal(conteudo, memoria.ToArray());
        }
        finally
        {
            if (Directory.Exists(diretorio)) Directory.Delete(diretorio, recursive: true);
        }
    }

    [Fact]
    public async Task Remover_ApagaOObjeto()
    {
        var storage = CriarStorage(out var diretorio);

        try
        {
            await storage.GravarAsync("chave-a", new MemoryStream([1, 2, 3]));
            await storage.RemoverAsync("chave-a");

            Assert.False(await storage.ExisteAsync("chave-a"));
        }
        finally
        {
            if (Directory.Exists(diretorio)) Directory.Delete(diretorio, recursive: true);
        }
    }

    // ── Retenção (P-06) ──────────────────────────────────────────────────────

    [Fact]
    public void AudioDeConsulta_TemRetencaoDeTrintaDias()
    {
        var audio = new Midia(TipoMidia.AudioConsulta, "audio/webm", consultaId: Guid.NewGuid());

        Assert.NotNull(audio.RetencaoAte);
        Assert.Equal(30, Math.Round((audio.RetencaoAte!.Value - audio.CriadaEm).TotalDays));
    }

    [Fact]
    public void ConteudoClinico_NaoExpira()
    {
        var documento = new Midia(TipoMidia.DocumentoPdf, "application/pdf");
        var exame = new Midia(TipoMidia.ResultadoExame, "application/pdf");

        // Guarda regulatoria do prontuario (RN-062)
        Assert.Null(documento.RetencaoAte);
        Assert.Null(exame.RetencaoAte);
    }

    [Fact]
    public void Midia_NasceAguardandoUpload()
    {
        var midia = new Midia(TipoMidia.FotoPet, "image/jpeg");

        Assert.False(midia.Disponivel());
        Assert.Equal(StatusMidia.AguardandoUpload, midia.Status);
    }

    [Fact]
    public void ConfirmarUpload_ArquivoVazio_NaoEAceito()
    {
        var midia = new Midia(TipoMidia.FotoPet, "image/jpeg");

        Assert.Throws<ArgumentOutOfRangeException>(() => midia.ConfirmarUpload(0));
    }

    // ── Contrato HTTP ────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadUrl_ContentTypeIncompativelComOTipo_Retorna400()
    {
        var token = await TokenDeResponsavelAsync();

        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/midia/upload-url")
        {
            // PDF nao e audio de consulta
            Content = Corpo("""{"tipo":"AudioConsulta","contentType":"application/pdf"}""")
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await _client.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task UploadUrl_TipoCompativel_DevolveMidiaIdEUrl()
    {
        var token = await TokenDeResponsavelAsync();

        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/midia/upload-url")
        {
            Content = Corpo("""{"tipo":"FotoPet","contentType":"image/jpeg"}""")
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await _client.SendAsync(requisicao);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        var url = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.NotEqual(Guid.Empty, url.GetProperty("midiaId").GetGuid());
        Assert.Contains("/api/storage/", url.GetProperty("uploadUrl").GetString());
    }

    [Fact]
    public async Task UploadUrl_SemToken_Retorna401()
    {
        var resposta = await _client.PostAsync("/api/midia/upload-url", Corpo(
            """{"tipo":"FotoPet","contentType":"image/jpeg"}"""));

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Storage_ComAssinaturaInvalida_Retorna403()
    {
        var resposta = await _client.GetAsync(
            "/api/storage/qualquer/chave?operacao=leitura&expiraEm=99999999999&assinatura=forjada");

        // A autorizacao e a propria assinatura da URL, como num bucket
        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    private static string ExtrairAssinatura(string url) =>
        Uri.UnescapeDataString(url.Split("assinatura=")[1].Split('&')[0]);

    private static long ExtrairExpiracao(string url) =>
        long.Parse(url.Split("expiraEm=")[1].Split('&')[0]);
}
