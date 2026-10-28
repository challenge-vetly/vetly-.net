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
[Collection(ColecaoDaApi.Nome)]
public class IdempotenciaTests
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

    /// <summary>
    /// Consultas em checkout do Responsável, cada uma num horário. A cobrança da rota
    /// pública é sempre de uma consulta (RN-006/RN-032), e uma consulta só aceita uma
    /// cobrança em aberto — duas cobranças distintas pedem duas consultas.
    /// </summary>
    private async Task<List<Guid>> ConsultasEmCheckoutAsync(string token, Guid tutorId, int quantidade = 1)
    {
        var animalId = await AtoresDeTeste.CriarPetAsync(_client, new AtoresDeTeste.Responsavel(tutorId, token));

        var criar = await AtoresDeTeste.EnviarAsync(_client, HttpMethod.Post, "/api/veterinarios",
            AtoresDeTeste.TokenAdmin(), $$"""
                {"nome":"Dra. Idempotente","crmv":"{{Random.Shared.Next(10000, 99999)}}-SP","ufAtuacao":"SP",
                 "email":"idem-vet-{{Guid.NewGuid():N}}@clinica.com","persona":"Autonomo","plano":"Profissional"}
                """);
        Assert.Equal(HttpStatusCode.Created, criar.StatusCode);
        var vetId = (await AtoresDeTeste.JsonDe(criar)).GetProperty("veterinario").GetProperty("id").GetGuid();
        var tokenVet = AtoresDeTeste.TokenVeterinario(vetId);

        await AtoresDeTeste.EnviarAsync(_client, HttpMethod.Put, $"/api/veterinarios/{vetId}/agenda-config", tokenVet,
            """{"dias":["Monday","Tuesday","Wednesday","Thursday","Friday","Saturday","Sunday"],"horaInicio":"08:00","horaFim":"18:00","duracaoMinutos":30,"intervaloMinutos":0}""");
        var servicos = await AtoresDeTeste.JsonDe(await AtoresDeTeste.EnviarAsync(_client, HttpMethod.Put,
            $"/api/veterinarios/{vetId}/servicos", tokenVet,
            """{"servicos":[{"tipo":"ConsultaRotina","valor":200.00,"duracaoMinutos":30,"aceitaPlanoPet":false}]}"""));
        var servicoId = servicos.EnumerateArray().First().GetProperty("id").GetGuid();

        var disponibilidade = await AtoresDeTeste.JsonDe(await AtoresDeTeste.EnviarAsync(_client, HttpMethod.Get,
            $"/api/veterinarios/{vetId}/disponibilidade", token));
        var slots = disponibilidade.GetProperty("dias").EnumerateArray()
            .SelectMany(d => d.GetProperty("horarios").EnumerateArray())
            .Select(h => h.GetProperty("id").GetGuid())
            .Take(quantidade);

        var consultas = new List<Guid>();
        foreach (var slotId in slots)
        {
            var checkout = await AtoresDeTeste.EnviarAsync(_client, HttpMethod.Post, "/api/consultas/checkout", token,
                $$"""{"animalId":"{{animalId}}","prestadorId":"{{vetId}}","slotId":"{{slotId}}","servicoId":"{{servicoId}}"}""");
            Assert.Equal(HttpStatusCode.Created, checkout.StatusCode);
            consultas.Add((await AtoresDeTeste.JsonDe(checkout)).GetProperty("consultaId").GetGuid());
        }

        return consultas;
    }

    private async Task<HttpResponseMessage> CriarCobrancaAsync(
        string token, Guid tutorId, Guid consultaId, string? chave)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/pagamentos")
        {
            Content = Corpo($$"""{"tutorId":"{{tutorId}}","consultaId":"{{consultaId}}","valor":200,"meioPagamento":"Pix"}""")
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

        // A chave e conferida antes de tudo: a consulta nem chega a ser procurada
        var resposta = await CriarCobrancaAsync(token, tutorId, Guid.NewGuid(), chave: null);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var problema = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("IDEMPOTENCIA-001", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task MesmaChave_NaoCriaDuasCobrancas()
    {
        var (token, tutorId) = await ResponsavelAsync();
        var consultaId = (await ConsultasEmCheckoutAsync(token, tutorId)).Single();
        var chave = Guid.NewGuid().ToString();

        var primeira = await CriarCobrancaAsync(token, tutorId, consultaId, chave);
        var segunda = await CriarCobrancaAsync(token, tutorId, consultaId, chave);

        Assert.Equal(HttpStatusCode.Accepted, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, segunda.StatusCode);

        var idDaPrimeira = (await primeira.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();
        var idDaSegunda = (await segunda.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetGuid();

        // A segunda chamada devolve a resposta da primeira, sem criar outra cobranca
        Assert.Equal(idDaPrimeira, idDaSegunda);
    }

    [Fact]
    public async Task MesmaChave_DevolveOCorpoIdenticoAoDaPrimeira()
    {
        var (token, tutorId) = await ResponsavelAsync();
        var consultaId = (await ConsultasEmCheckoutAsync(token, tutorId)).Single();
        var chave = Guid.NewGuid().ToString();

        var primeira = await (await CriarCobrancaAsync(token, tutorId, consultaId, chave)).Content.ReadAsStringAsync();
        var segunda = await (await CriarCobrancaAsync(token, tutorId, consultaId, chave)).Content.ReadAsStringAsync();

        // O reenvio serializava com opcoes proprias e perdia o conversor de enum:
        // "statusPagamento" voltava 1 em vez de "Pendente". Mesma resposta e mesmo
        // contrato, byte a byte.
        Assert.Equal(primeira, segunda);
        Assert.Equal("Pendente",
            JsonDocument.Parse(segunda).RootElement.GetProperty("statusPagamento").GetString());
    }

    [Fact]
    public async Task ChavesDiferentes_CriamCobrancasDiferentes()
    {
        var (token, tutorId) = await ResponsavelAsync();

        var consultas = await ConsultasEmCheckoutAsync(token, tutorId, quantidade: 2);

        var primeira = await CriarCobrancaAsync(token, tutorId, consultas[0], Guid.NewGuid().ToString());
        var segunda = await CriarCobrancaAsync(token, tutorId, consultas[1], Guid.NewGuid().ToString());

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

        var consultaDaAna = (await ConsultasEmCheckoutAsync(tokenDaAna, tutorDaAna)).Single();
        var consultaDoBruno = (await ConsultasEmCheckoutAsync(tokenDoBruno, tutorDoBruno)).Single();

        var daAna = await CriarCobrancaAsync(tokenDaAna, tutorDaAna, consultaDaAna, chave);
        var doBruno = await CriarCobrancaAsync(tokenDoBruno, tutorDoBruno, consultaDoBruno, chave);

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

        var resposta = await CriarCobrancaAsync(token, tutorId, Guid.NewGuid(), new string('k', 101));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }
}
