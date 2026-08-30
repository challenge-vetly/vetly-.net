using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Vetly.IntegrationTests;

/// <summary>
/// Isolamento de dados entre Responsaveis (conflito C-07; RN-069, RN-105, RN-106).
///
/// Ate a onda 2 qualquer usuario autenticado listava tutores, animais, consultas e
/// pagamentos de todo mundo. Estes testes sao a prova de que o escopo por linha
/// fecha isso — e a rede de seguranca contra regressao.
/// </summary>
public class IsolamentoDeDadosTests : IClassFixture<VetlyWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public IsolamentoDeDadosTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    private static StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>Um Responsavel cadastrado, com token e um pet.</summary>
    private sealed record Responsavel(Guid TutorId, string Token, Guid AnimalId);

    private async Task<Responsavel> CriarResponsavelComPetAsync(string nome)
    {
        var email = $"{nome}-{Guid.NewGuid():N}@exemplo.com";

        var registro = await _client.PostAsync("/api/auth/registro/tutor", Corpo(
            $$"""
            {"nome":"{{nome}}","email":"{{email}}","telefone":"11999998888","senha":"senha-forte-123"}
            """));
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

        var sessao = await registro.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = sessao.GetProperty("token").GetString()!;
        var tutorId = sessao.GetProperty("tutorId").GetGuid();

        // Consentimento antes de qualquer acao de negocio — e o caminho real do app (RN-060)
        var consentir = new HttpRequestMessage(HttpMethod.Put, $"/api/tutores/{tutorId}/consentimentos")
        {
            Content = Corpo("""{"consentimentos":[{"finalidade":"Atendimento","concedido":true}]}""")
        };
        consentir.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await _client.SendAsync(consentir)).StatusCode);

        var criarPet = new HttpRequestMessage(HttpMethod.Post, "/api/animais")
        {
            Content = Corpo($$"""
                {"nome":"Pet de {{nome}}","especie":"Canino","raca":"SRD",
                 "dataNascimento":"2023-04-10T00:00:00Z","tutorId":"{{tutorId}}","pesoKg":12.5}
                """)
        };
        criarPet.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respostaPet = await _client.SendAsync(criarPet);
        Assert.Equal(HttpStatusCode.Created, respostaPet.StatusCode);

        var pet = await respostaPet.Content.ReadFromJsonAsync<JsonElement>(Json);
        return new Responsavel(tutorId, token, pet.GetProperty("id").GetGuid());
    }

    private async Task<HttpResponseMessage> GetComTokenAsync(string rota, string token)
    {
        var requisicao = new HttpRequestMessage(HttpMethod.Get, rota);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(requisicao);
    }

    // ── Animais (RN-105) ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListagemDeAnimais_SoDevolveOsPetsDoProprioResponsavel()
    {
        var ana = await CriarResponsavelComPetAsync("Ana");
        var bruno = await CriarResponsavelComPetAsync("Bruno");

        var resposta = await GetComTokenAsync("/api/animais", ana.Token);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var animais = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        var ids = animais.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ToList();

        Assert.Contains(ana.AnimalId, ids);
        Assert.DoesNotContain(bruno.AnimalId, ids);
    }

    [Fact]
    public async Task DetalheDeAnimalDeOutroResponsavel_Retorna403()
    {
        var ana = await CriarResponsavelComPetAsync("Ana");
        var bruno = await CriarResponsavelComPetAsync("Bruno");

        var resposta = await GetComTokenAsync($"/api/animais/{bruno.AnimalId}", ana.Token);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task ProntuariosDeAnimalDeOutroResponsavel_Retorna403()
    {
        var ana = await CriarResponsavelComPetAsync("Ana");
        var bruno = await CriarResponsavelComPetAsync("Bruno");

        // Dado clinico e sensivel (RN-069): nao basta esconder da listagem
        var resposta = await GetComTokenAsync($"/api/animais/{bruno.AnimalId}/prontuarios", ana.Token);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task ExamesDeAnimalDeOutroResponsavel_Retorna403()
    {
        var ana = await CriarResponsavelComPetAsync("Ana");
        var bruno = await CriarResponsavelComPetAsync("Bruno");

        var resposta = await GetComTokenAsync($"/api/animais/{bruno.AnimalId}/exames", ana.Token);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task CadastrarPetNoNomeDeOutroResponsavel_Retorna403()
    {
        var ana = await CriarResponsavelComPetAsync("Ana");
        var bruno = await CriarResponsavelComPetAsync("Bruno");

        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/animais")
        {
            Content = Corpo($$"""
                {"nome":"Pet infiltrado","especie":"Canino","raca":"SRD",
                 "dataNascimento":"2023-04-10T00:00:00Z","tutorId":"{{bruno.TutorId}}","pesoKg":10}
                """)
        };
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ana.Token);

        var resposta = await _client.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    // ── Tutores (RN-106) ─────────────────────────────────────────────────────

    [Fact]
    public async Task CadastroDeOutroResponsavel_Retorna403()
    {
        var ana = await CriarResponsavelComPetAsync("Ana");
        var bruno = await CriarResponsavelComPetAsync("Bruno");

        var resposta = await GetComTokenAsync($"/api/tutores/{bruno.TutorId}", ana.Token);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task ProprioCadastro_Retorna200()
    {
        var ana = await CriarResponsavelComPetAsync("Ana");

        var resposta = await GetComTokenAsync($"/api/tutores/{ana.TutorId}", ana.Token);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task AnimaisDeOutroResponsavel_PelaRotaDoTutor_Retorna403()
    {
        var ana = await CriarResponsavelComPetAsync("Ana");
        var bruno = await CriarResponsavelComPetAsync("Bruno");

        var resposta = await GetComTokenAsync($"/api/tutores/{bruno.TutorId}/animais", ana.Token);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task ListagemDeTutores_ContinuaRestritaAAdmin()
    {
        var ana = await CriarResponsavelComPetAsync("Ana");

        var resposta = await GetComTokenAsync("/api/tutores", ana.Token);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    // ── Consultas e pagamentos (RN-105/RN-106) ───────────────────────────────

    [Fact]
    public async Task ListagemDeConsultas_NaoVazaConsultaDeOutroResponsavel()
    {
        var ana = await CriarResponsavelComPetAsync("Ana");
        var bruno = await CriarResponsavelComPetAsync("Bruno");

        // Mesmo pedindo explicitamente o tutorId do Bruno na query string, o escopo do
        // token prevalece — senao bastaria trocar o id da URL
        var resposta = await GetComTokenAsync($"/api/consultas?tutorId={bruno.TutorId}", ana.Token);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var pagina = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        foreach (var consulta in pagina.GetProperty("itens").EnumerateArray())
            Assert.Equal(ana.TutorId, consulta.GetProperty("tutorId").GetGuid());
    }

    [Fact]
    public async Task ListagemDePagamentos_NaoVazaPagamentoDeOutroResponsavel()
    {
        var ana = await CriarResponsavelComPetAsync("Ana");

        var resposta = await GetComTokenAsync("/api/pagamentos", ana.Token);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var pagina = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        foreach (var pagamento in pagina.GetProperty("itens").EnumerateArray())
            Assert.Equal(ana.TutorId, pagamento.GetProperty("tutorId").GetGuid());
    }
}
