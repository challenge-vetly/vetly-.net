using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Vetly.IntegrationTests;

/// <summary>
/// Idempotencia das rotas que nao podem executar duas vezes (§2.5).
///
/// O app repete envio por natureza: rede oscila, o usuario toca de novo, o cliente
/// faz retry. Reservar horario e criar cobranca nao podem acontecer em dobro.
/// </summary>
public class IdempotenciaTests : IClassFixture<VetlyWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IdempotenciaTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    private static StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<(string Token, Guid TutorId)> ResponsavelAsync()
    {
        var email = $"idem-{Guid.NewGuid():N}@exemplo.com";

        var registro = await _client.PostAsync("/api/auth/registro/tutor", Corpo(
            $$"""
            {"nome":"Ana","email":"{{email}}","telefone":"11999998888","senha":"senha-forte-123"}
            """));

        var sessao = await registro.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = sessao.GetProperty("token").GetString()!;
        var tutorId = sessao.GetProperty("tutorId").GetGuid();

        var consentir = new HttpRequestMessage(HttpMethod.Put, $"/api/tutores/{tutorId}/consentimentos")
        {
            Content = Corpo("""{"consentimentos":[{"finalidade":"Atendimento","concedido":true}]}""")
        };
        consentir.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(consentir);

        return (token, tutorId);
    }

    private async Task<HttpResponseMessage> CriarCobrancaAsync(
        string token, Guid tutorId, decimal valor, string? chave)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/pagamentos")
        {
            Content = Corpo($$"""{"tutorId":"{{tutorId}}","valor":{{valor}},"meioPagamento":"Pix"}""")
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (chave is not null)
            requisicao.Headers.Add("Idempotency-Key", chave);

        return await _client.SendAsync(requisicao);
    }

    [Fact]
    public async Task RotaIdempotente_SemOHeader_Retorna400()
    {
        var (token, tutorId) = await ResponsavelAsync();

        var resposta = await CriarCobrancaAsync(token, tutorId, 200m, chave: null);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("IDEMPOTENCIA-001", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task MesmaChave_NaoCriaDuasCobrancas()
    {
        var (token, tutorId) = await ResponsavelAsync();
        var chave = Guid.NewGuid().ToString();

        var primeira = await CriarCobrancaAsync(token, tutorId, 200m, chave);
        var segunda = await CriarCobrancaAsync(token, tutorId, 200m, chave);

        Assert.Equal(HttpStatusCode.Accepted, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, segunda.StatusCode);

        var idDaPrimeira = (await primeira.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var idDaSegunda = (await segunda.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        // A segunda chamada devolve a resposta da primeira, sem criar outra cobranca
        Assert.Equal(idDaPrimeira, idDaSegunda);
    }

    [Fact]
    public async Task ChavesDiferentes_CriamCobrancasDiferentes()
    {
        var (token, tutorId) = await ResponsavelAsync();

        var primeira = await CriarCobrancaAsync(token, tutorId, 200m, Guid.NewGuid().ToString());
        var segunda = await CriarCobrancaAsync(token, tutorId, 200m, Guid.NewGuid().ToString());

        var idDaPrimeira = (await primeira.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var idDaSegunda = (await segunda.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        // Duas intencoes de pagamento distintas continuam sendo duas cobrancas
        Assert.NotEqual(idDaPrimeira, idDaSegunda);
    }

    [Fact]
    public async Task MesmaChaveDeOutroResponsavel_NaoReaproveitaAResposta()
    {
        var chave = Guid.NewGuid().ToString();

        var (tokenDaAna, tutorDaAna) = await ResponsavelAsync();
        var (tokenDoBruno, tutorDoBruno) = await ResponsavelAsync();

        var daAna = await CriarCobrancaAsync(tokenDaAna, tutorDaAna, 200m, chave);
        var doBruno = await CriarCobrancaAsync(tokenDoBruno, tutorDoBruno, 200m, chave);

        var idDaAna = (await daAna.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var idDoBruno = (await doBruno.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        // A chave e do trio (chave, usuario, rota): a mesma string de outra pessoa e
        // outra requisicao, senao um cliente descuidado bloquearia o de outro
        Assert.NotEqual(idDaAna, idDoBruno);
    }

    [Fact]
    public async Task RotaNaoIdempotente_NaoExigeOHeader()
    {
        var (token, _) = await ResponsavelAsync();

        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/animais");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resposta = await _client.SendAsync(requisicao);

        // O filtro so age onde ha [Idempotente]
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task ChaveLongaDemais_Retorna400()
    {
        var (token, tutorId) = await ResponsavelAsync();

        var resposta = await CriarCobrancaAsync(token, tutorId, 200m, new string('k', 101));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
