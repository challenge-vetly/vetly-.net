using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Vetly.IntegrationTests;

/// <summary>
/// Portao de consentimento LGPD (RN-060 a RN-062): a base legal precede o
/// tratamento de dados, e o consentimento e granular por finalidade e revogavel.
/// </summary>
public class ConsentimentoLgpdTests : IClassFixture<VetlyWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ConsentimentoLgpdTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    private static StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

    private sealed record Sessao(Guid TutorId, string Token);

    private async Task<Sessao> RegistrarAsync()
    {
        var email = $"lgpd-{Guid.NewGuid():N}@exemplo.com";
        var resposta = await _client.PostAsync("/api/auth/registro/tutor", Corpo(
            $$"""
            {"nome":"Ana","email":"{{email}}","telefone":"11999998888","senha":"senha-forte-123"}
            """));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var sessao = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);

        return new Sessao(sessao.GetProperty("tutorId").GetGuid(), sessao.GetProperty("token").GetString()!);
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string rota, string token, string? corpo = null)
    {
        var requisicao = new HttpRequestMessage(metodo, rota);
        if (corpo is not null) requisicao.Content = Corpo(corpo);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(requisicao);
    }

    private Task<HttpResponseMessage> ConsentirAtendimentoAsync(Sessao sessao, bool concedido = true) =>
        EnviarAsync(HttpMethod.Put, $"/api/tutores/{sessao.TutorId}/consentimentos", sessao.Token,
            $$"""{"consentimentos":[{"finalidade":"Atendimento","concedido":{{(concedido ? "true" : "false")}}}]}""");

    // ── O portão (RN-060) ────────────────────────────────────────────────────

    [Fact]
    public async Task RotaDeNegocio_SemConsentimento_Retorna422ComCodigoRN060()
    {
        var sessao = await RegistrarAsync();

        var resposta = await EnviarAsync(HttpMethod.Get, "/api/animais", sessao.Token);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);

        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("RN-060", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task RotaDeNegocio_ComConsentimento_Libera()
    {
        var sessao = await RegistrarAsync();
        await ConsentirAtendimentoAsync(sessao);

        var resposta = await EnviarAsync(HttpMethod.Get, "/api/animais", sessao.Token);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task RotasDeConsentimentoEPerfil_FuncionamAntesDeConsentir()
    {
        var sessao = await RegistrarAsync();

        // Sem estas o Responsavel ficaria travado: nao teria como chegar a consentir
        var consentimentos = await EnviarAsync(HttpMethod.Get, $"/api/tutores/{sessao.TutorId}/consentimentos", sessao.Token);
        var perfil = await EnviarAsync(HttpMethod.Get, "/api/auth/me", sessao.Token);
        var cadastro = await EnviarAsync(HttpMethod.Get, $"/api/tutores/{sessao.TutorId}", sessao.Token);

        Assert.Equal(HttpStatusCode.OK, consentimentos.StatusCode);
        Assert.Equal(HttpStatusCode.OK, perfil.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cadastro.StatusCode);
    }

    [Fact]
    public async Task RevogarAtendimento_VoltaABloquearAsRotasDeNegocio()
    {
        var sessao = await RegistrarAsync();
        await ConsentirAtendimentoAsync(sessao);
        Assert.Equal(HttpStatusCode.OK, (await EnviarAsync(HttpMethod.Get, "/api/animais", sessao.Token)).StatusCode);

        await ConsentirAtendimentoAsync(sessao, concedido: false);

        var depois = await EnviarAsync(HttpMethod.Get, "/api/animais", sessao.Token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, depois.StatusCode);
    }

    // ── Granularidade e datas (RN-061/RN-062) ────────────────────────────────

    [Fact]
    public async Task Consentimentos_ListamAsCincoFinalidadesComDescricao()
    {
        var sessao = await RegistrarAsync();

        var resposta = await EnviarAsync(HttpMethod.Get, $"/api/tutores/{sessao.TutorId}/consentimentos", sessao.Token);
        var lista = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);

        var finalidades = lista.EnumerateArray().Select(c => c.GetProperty("finalidade").GetString()).ToList();

        Assert.Equal(5, finalidades.Count);
        Assert.Contains("Atendimento", finalidades);
        Assert.Contains("Compartilhamento", finalidades);
        Assert.Contains("Promocoes", finalidades);
        Assert.Contains("DadosAgregados", finalidades);
        // A finalidade tem que ser apresentada de forma clara, nao so nomeada (RN-060)
        Assert.All(lista.EnumerateArray(),
            c => Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("descricao").GetString())));
    }

    [Fact]
    public async Task AtualizarConsentimentos_NaoRevogaPorOmissao()
    {
        var sessao = await RegistrarAsync();

        await EnviarAsync(HttpMethod.Put, $"/api/tutores/{sessao.TutorId}/consentimentos", sessao.Token,
            """{"consentimentos":[{"finalidade":"Atendimento","concedido":true},{"finalidade":"Promocoes","concedido":true}]}""");

        // Altera so uma finalidade: a outra tem que permanecer como estava (RN-061)
        var resposta = await EnviarAsync(HttpMethod.Put, $"/api/tutores/{sessao.TutorId}/consentimentos", sessao.Token,
            """{"consentimentos":[{"finalidade":"Promocoes","concedido":false}]}""");

        var lista = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        var porFinalidade = lista.EnumerateArray()
            .ToDictionary(c => c.GetProperty("finalidade").GetString()!, c => c);

        Assert.True(porFinalidade["Atendimento"].GetProperty("concedido").GetBoolean());
        Assert.False(porFinalidade["Promocoes"].GetProperty("concedido").GetBoolean());
    }

    [Fact]
    public async Task AtualizarConsentimentos_RegistraDataDeConcessaoEDeRevogacao()
    {
        var sessao = await RegistrarAsync();

        await ConsentirAtendimentoAsync(sessao);
        var resposta = await ConsentirAtendimentoAsync(sessao, concedido: false);

        var lista = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        var atendimento = lista.EnumerateArray()
            .Single(c => c.GetProperty("finalidade").GetString() == "Atendimento");

        Assert.False(atendimento.GetProperty("concedido").GetBoolean());
        // A revogacao nao apaga o historico da concessao (RN-062)
        Assert.NotEqual(JsonValueKind.Null, atendimento.GetProperty("concedidoEm").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, atendimento.GetProperty("revogadoEm").ValueKind);
    }

    [Fact]
    public async Task Consentimentos_DeOutroResponsavel_Retorna403()
    {
        var ana = await RegistrarAsync();
        var bruno = await RegistrarAsync();

        var resposta = await EnviarAsync(HttpMethod.Get, $"/api/tutores/{bruno.TutorId}/consentimentos", ana.Token);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }
}
