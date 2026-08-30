using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Vetly.IntegrationTests;

/// <summary>
/// Credencial do veterinario (pendencia P-05) e encerramento de acesso no
/// offboarding (RN-022/RN-024), por HTTP.
/// </summary>
public class AuthVeterinarioTests : IClassFixture<VetlyWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AuthVeterinarioTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    private static StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>Vet recem-cadastrado pelo Admin, com a senha temporaria da resposta.</summary>
    private sealed record VetCadastrado(Guid Id, string Email, string SenhaTemporaria);

    private async Task<string> TokenDeAdminAsync()
    {
        var resposta = await _client.PostAsync("/api/auth/token", Corpo(
            """{"usuario":"admin-teste","role":"Admin"}"""));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var sessao = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
        return sessao.GetProperty("token").GetString()!;
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo, string rota, string token, string? corpo = null)
    {
        var requisicao = new HttpRequestMessage(metodo, rota);
        if (corpo is not null) requisicao.Content = Corpo(corpo);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(requisicao);
    }

    /// <summary>Cadastra um vet pelo Admin. O CRMV terminando em 5 e valido no adaptador simulado.</summary>
    private async Task<VetCadastrado> CadastrarVetAsync()
    {
        var admin = await TokenDeAdminAsync();
        var email = $"vet-{Guid.NewGuid():N}@exemplo.com";
        var crmv = $"{Random.Shared.Next(10000, 99999) / 10 * 10 + 5}-SP";

        var resposta = await EnviarAsync(HttpMethod.Post, "/api/veterinarios", admin,
            $$"""
            {"nome":"Dra. Marina","crmv":"{{crmv}}","ufAtuacao":"SP","email":"{{email}}",
             "persona":"Autonomo","plano":"Profissional"}
            """);

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criado = await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);

        return new VetCadastrado(
            criado.GetProperty("veterinario").GetProperty("id").GetGuid(),
            criado.GetProperty("email").GetString()!,
            criado.GetProperty("senhaTemporaria").GetString()!);
    }

    // ── Senha temporária (P-05) ──────────────────────────────────────────────

    [Fact]
    public async Task CadastroDeVet_DevolveSenhaTemporariaUmaUnicaVez()
    {
        var vet = await CadastrarVetAsync();

        Assert.False(string.IsNullOrWhiteSpace(vet.SenhaTemporaria));

        // A senha nao pode reaparecer em nenhuma outra rota
        var admin = await TokenDeAdminAsync();
        var consulta = await EnviarAsync(HttpMethod.Get, $"/api/veterinarios/{vet.Id}", admin);
        var corpo = await consulta.Content.ReadAsStringAsync();

        Assert.DoesNotContain(vet.SenhaTemporaria, corpo);
        Assert.DoesNotContain("senhaHash", corpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_ComSenhaTemporaria_AutenticaEAvisaQueEProvisoria()
    {
        var vet = await CadastrarVetAsync();

        var login = await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{vet.Email}}","senha":"{{vet.SenhaTemporaria}}"}"""));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var sessao = await login.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal("Veterinario", sessao.GetProperty("role").GetString());
        Assert.Equal(vet.Id, sessao.GetProperty("veterinarioId").GetGuid());
        Assert.True(sessao.GetProperty("senhaTemporaria").GetBoolean());
    }

    [Fact]
    public async Task TrocarSenha_TiraAMarcaDeTemporariaEInvalidaASenhaAntiga()
    {
        var vet = await CadastrarVetAsync();

        var login = await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{vet.Email}}","senha":"{{vet.SenhaTemporaria}}"}"""));
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("token").GetString()!;

        var troca = await EnviarAsync(HttpMethod.Post, "/api/auth/trocar-senha", token,
            $$"""{"senhaAtual":"{{vet.SenhaTemporaria}}","novaSenha":"nova-senha-forte-123"}""");
        Assert.Equal(HttpStatusCode.NoContent, troca.StatusCode);

        // A antiga deixa de valer
        var comAntiga = await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{vet.Email}}","senha":"{{vet.SenhaTemporaria}}"}"""));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, comAntiga.StatusCode);

        // A nova vale, e a marca de temporaria sumiu
        var comNova = await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{vet.Email}}","senha":"nova-senha-forte-123"}"""));
        Assert.Equal(HttpStatusCode.OK, comNova.StatusCode);

        var sessao = await comNova.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.False(sessao.GetProperty("senhaTemporaria").GetBoolean());
    }

    [Fact]
    public async Task TrocarSenha_ComSenhaAtualErrada_Retorna422()
    {
        var vet = await CadastrarVetAsync();

        var login = await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{vet.Email}}","senha":"{{vet.SenhaTemporaria}}"}"""));
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("token").GetString()!;

        var troca = await EnviarAsync(HttpMethod.Post, "/api/auth/trocar-senha", token,
            """{"senhaAtual":"chute-errado","novaSenha":"nova-senha-forte-123"}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, troca.StatusCode);
    }

    [Fact]
    public async Task CadastroDeVet_ComEmailRepetido_Retorna422()
    {
        var vet = await CadastrarVetAsync();
        var admin = await TokenDeAdminAsync();

        var resposta = await EnviarAsync(HttpMethod.Post, "/api/veterinarios", admin,
            $$"""
            {"nome":"Outro","crmv":"33335-SP","ufAtuacao":"SP","email":"{{vet.Email}}",
             "persona":"Autonomo","plano":"Basico"}
            """);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    // ── Offboarding (RN-022/RN-024) ──────────────────────────────────────────

    [Fact]
    public async Task VetDesativado_EntraComRoleReduzidaENaoAlcancaRotaDeNegocio()
    {
        var vet = await CadastrarVetAsync();
        var admin = await TokenDeAdminAsync();

        var desativacao = await EnviarAsync(HttpMethod.Delete, $"/api/veterinarios/{vet.Id}", admin);
        Assert.Equal(HttpStatusCode.OK, desativacao.StatusCode);

        // Ele ainda entra — precisa, para pedir o extrato dos proprios atendimentos (RN-024)
        var login = await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{vet.Email}}","senha":"{{vet.SenhaTemporaria}}"}"""));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var sessao = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("VetDesativado", sessao.GetProperty("role").GetString());

        // Mas o acesso a plataforma acabou (RN-022)
        var token = sessao.GetProperty("token").GetString()!;
        var animais = await EnviarAsync(HttpMethod.Get, "/api/animais", token);

        Assert.Equal(HttpStatusCode.Forbidden, animais.StatusCode);

        var problema = await animais.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("RN-024", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task VetDesativado_AindaConsegueVerOProprioPerfil()
    {
        var vet = await CadastrarVetAsync();
        var admin = await TokenDeAdminAsync();
        await EnviarAsync(HttpMethod.Delete, $"/api/veterinarios/{vet.Id}", admin);

        var login = await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{vet.Email}}","senha":"{{vet.SenhaTemporaria}}"}"""));
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("token").GetString()!;

        // As rotas de sessao seguem abertas: sem elas ele nao teria como pedir o extrato
        var perfil = await EnviarAsync(HttpMethod.Get, "/api/auth/me", token);

        Assert.Equal(HttpStatusCode.OK, perfil.StatusCode);
        var corpo = await perfil.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("VetDesativado", corpo.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Perfil_DoVet_ListaAsPendenciasDele()
    {
        var vet = await CadastrarVetAsync();

        var login = await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{vet.Email}}","senha":"{{vet.SenhaTemporaria}}"}"""));
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("token").GetString()!;

        var perfil = await EnviarAsync(HttpMethod.Get, "/api/auth/me", token);
        var corpo = await perfil.Content.ReadFromJsonAsync<JsonElement>(Json);
        var pendencias = corpo.GetProperty("pendencias").EnumerateArray()
            .Select(p => p.GetString()).ToList();

        Assert.Contains("SenhaTemporaria", pendencias);
        Assert.Contains("EnderecoNaoInformado", pendencias);
    }
}
