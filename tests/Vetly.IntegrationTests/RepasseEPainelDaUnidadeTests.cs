using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Vetly.IntegrationTests;

/// <summary>
/// Rotas de onboarding financeiro do prestador (§4.1) e painel da unidade (§5.2),
/// pela API real.
///
/// Os testes de unidade ja cobrem as regras. O que so aparece aqui e o que vem do
/// pipeline: a policy que barra a rota antes do servico, o DTO que desserializa, e o
/// 400 que a validacao de modelo produz — coisas que um mock de <c>IUsuarioAtual</c>
/// nunca exercita.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class RepasseEPainelDaUnidadeTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public RepasseEPainelDaUnidadeTests(VetlyWebApplicationFactory factory) =>
        _client = factory.CreateClient();

    private static StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo, string rota, string token, string? corpo = null)
    {
        var requisicao = new HttpRequestMessage(metodo, rota);
        if (corpo is not null) requisicao.Content = Corpo(corpo);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(requisicao);
    }

    private async Task<string> TokenDeAdminAsync()
    {
        var resposta = await _client.PostAsync("/api/auth/token", Corpo(
            """{"usuario":"admin-repasse","role":"Admin"}"""));

        var sessao = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        return sessao.GetProperty("token").GetString()!;
    }

    /// <summary>Vet cadastrado e autenticado, com o token dele. O CRMV terminado em 5 e valido.</summary>
    private async Task<string> TokenDeVeterinarioAsync()
    {
        var admin = await TokenDeAdminAsync();
        var email = $"vet-{Guid.NewGuid():N}@exemplo.com";
        var crmv = $"{Random.Shared.Next(10000, 99999) / 10 * 10 + 5}-SP";

        var criacao = await EnviarAsync(HttpMethod.Post, "/api/veterinarios", admin,
            $$"""
            {"nome":"Dra. Marina","crmv":"{{crmv}}","ufAtuacao":"SP","email":"{{email}}",
             "persona":"Autonomo","plano":"Profissional"}
            """);

        Assert.Equal(HttpStatusCode.Created, criacao.StatusCode);
        var criado = await criacao.Content.ReadFromJsonAsync<JsonElement>(Json);
        var senha = criado.GetProperty("senhaTemporaria").GetString()!;

        var login = await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{email}}","senha":"{{senha}}"}"""));

        var sessao = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        return sessao.GetProperty("token").GetString()!;
    }

    private const string ContaValida =
        """
        {"banco":"341","agencia":"1234","conta":"0012345-6",
         "documentoTitular":"123.456.789-09","chavePix":"marina@clinicavetly.com.br"}
        """;

    // ── Conta de repasse do veterinario (§4.1, §7.3) ─────────────────────────

    [Fact]
    public async Task SemConta_ARotaRespondeConfiguradoFalso()
    {
        var vet = await TokenDeVeterinarioAsync();

        var resposta = await EnviarAsync(HttpMethod.Get, "/api/veterinarios/me/dados-repasse", vet);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.False(corpo.GetProperty("configurado").GetBoolean());
    }

    [Fact]
    public async Task DefinirEDepoisLer_DevolveAContaMascarada()
    {
        var vet = await TokenDeVeterinarioAsync();

        var definir = await EnviarAsync(
            HttpMethod.Put, "/api/veterinarios/me/dados-repasse", vet, ContaValida);

        Assert.Equal(HttpStatusCode.OK, definir.StatusCode);

        var leitura = await EnviarAsync(HttpMethod.Get, "/api/veterinarios/me/dados-repasse", vet);
        var texto = await leitura.Content.ReadAsStringAsync();

        // O numero da conta e a chave Pix nunca voltam inteiros (§7.3)
        Assert.DoesNotContain("0012345-6", texto);
        Assert.DoesNotContain("marina@clinicavetly.com.br", texto);
        Assert.Contains("341", texto);
    }

    [Fact]
    public async Task DocumentoDoTitularInvalido_Responde400()
    {
        var vet = await TokenDeVeterinarioAsync();

        var resposta = await EnviarAsync(HttpMethod.Put, "/api/veterinarios/me/dados-repasse", vet,
            """
            {"banco":"341","agencia":"1234","conta":"0012345-6",
             "documentoTitular":"123","chavePix":"marina@vetly.com"}
            """);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    [Fact]
    public async Task AdminSemCadastroProfissional_NaoAlcancaAContaDeNinguem()
    {
        // Nao ha id de veterinario na rota, e e por isso que a §7.3 se sustenta: nao
        // existe Guid para o Admin trocar.
        var admin = await TokenDeAdminAsync();

        var resposta = await EnviarAsync(HttpMethod.Get, "/api/veterinarios/me/dados-repasse", admin);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task SemToken_ARotaExigeAutenticacao()
    {
        var resposta = await _client.GetAsync("/api/veterinarios/me/dados-repasse");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    // ── Painel da unidade (§5.2, §7.3) ───────────────────────────────────────

    [Fact]
    public async Task PainelDaUnidade_ExigeAdmin()
    {
        var vet = await TokenDeVeterinarioAsync();

        var resposta = await EnviarAsync(HttpMethod.Get, "/api/dashboard/unidade", vet);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task PainelDaUnidade_SemToken_ExigeAutenticacao()
    {
        var resposta = await _client.GetAsync("/api/dashboard/unidade");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task PainelDaUnidade_AdminSemCadastroProfissional_ENegado()
    {
        // O token de desenvolvimento emite um Admin sem claim de veterinario. Sem ela
        // nao ha vinculo do qual derivar a unidade — e, como nao ha id na rota, tambem
        // nao ha por onde pedir a de outra clinica (§7.3).
        var admin = await TokenDeAdminAsync();

        var resposta = await EnviarAsync(HttpMethod.Get, "/api/dashboard/unidade", admin);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }
}
