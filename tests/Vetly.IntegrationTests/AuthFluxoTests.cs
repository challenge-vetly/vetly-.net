using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Vetly.IntegrationTests;

/// <summary>
/// Fluxo ponta a ponta da sessao do Responsavel por HTTP (§3.1):
/// cadastro, login, /me, rotacao do refresh token e logout.
/// </summary>
public class AuthFluxoTests : IClassFixture<VetlyWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web);

    public AuthFluxoTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    private static StringContent Corpo(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static string EmailUnico() => $"tutor-{Guid.NewGuid():N}@exemplo.com";

    private async Task<JsonElement> RegistrarAsync(string email, string senha = "senha-forte-123")
    {
        var resposta = await _client.PostAsync("/api/auth/registro/tutor", Corpo(
            $$"""
            {"nome":"Ana Teste","email":"{{email}}","telefone":"11999998888","senha":"{{senha}}"}
            """));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        return await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    [Fact]
    public async Task Registro_DevolveSessaoComConsentimentoPendente()
    {
        var sessao = await RegistrarAsync(EmailUnico());

        Assert.False(string.IsNullOrWhiteSpace(sessao.GetProperty("token").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(sessao.GetProperty("refreshToken").GetString()));
        Assert.Equal("Tutor", sessao.GetProperty("role").GetString());
        // RN-060: a base legal precede o tratamento — o cadastro nasce sem consentimento
        Assert.True(sessao.GetProperty("consentimentoPendente").GetBoolean());
    }

    [Fact]
    public async Task Registro_EmailRepetido_Retorna422()
    {
        var email = EmailUnico();
        await RegistrarAsync(email);

        var resposta = await _client.PostAsync("/api/auth/registro/tutor", Corpo(
            $$"""
            {"nome":"Outra","email":"{{email}}","telefone":"11888887777","senha":"senha-forte-123"}
            """));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    [Fact]
    public async Task Login_ComCredenciaisCorretas_AutenticaEDaAcessoAoMe()
    {
        var email = EmailUnico();
        await RegistrarAsync(email);

        var login = await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{email}}","senha":"senha-forte-123"}"""));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var sessao = await login.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = sessao.GetProperty("token").GetString();

        var comToken = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        comToken.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await _client.SendAsync(comToken);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var perfil = await me.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(email, perfil.GetProperty("email").GetString());
        Assert.Equal("Tutor", perfil.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_ComSenhaErrada_Retorna422()
    {
        var email = EmailUnico();
        await RegistrarAsync(email);

        var resposta = await _client.PostAsync("/api/auth/login", Corpo(
            $$"""{"email":"{{email}}","senha":"senha-errada"}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);
    }

    [Fact]
    public async Task Me_SemToken_Retorna401()
    {
        var resposta = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Me_ComTokenInvalido_Retorna401()
    {
        // O 401 tem que vir da validacao do token pelo middleware, nao de checagem
        // manual dentro da action
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token.invalido.qualquer");

        var resposta = await _client.SendAsync(requisicao);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotacionaOTokenEInvalidaOAnterior()
    {
        var sessao = await RegistrarAsync(EmailUnico());
        var refreshOriginal = sessao.GetProperty("refreshToken").GetString();

        var primeira = await _client.PostAsync("/api/auth/refresh", Corpo(
            $$"""{"refreshToken":"{{refreshOriginal}}"}"""));
        Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);

        var renovada = await primeira.Content.ReadFromJsonAsync<JsonElement>(Json);
        var refreshNovo = renovada.GetProperty("refreshToken").GetString();
        Assert.NotEqual(refreshOriginal, refreshNovo);

        // Reapresentar o token ja rotacionado nao renova nada
        var reuso = await _client.PostAsync("/api/auth/refresh", Corpo(
            $$"""{"refreshToken":"{{refreshOriginal}}"}"""));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reuso.StatusCode);
    }

    [Fact]
    public async Task Logout_RevogaORefreshToken()
    {
        var sessao = await RegistrarAsync(EmailUnico());
        var refresh = sessao.GetProperty("refreshToken").GetString();

        var logout = await _client.PostAsync("/api/auth/logout", Corpo(
            $$"""{"refreshToken":"{{refresh}}"}"""));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var depois = await _client.PostAsync("/api/auth/refresh", Corpo(
            $$"""{"refreshToken":"{{refresh}}"}"""));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, depois.StatusCode);
    }

    [Fact]
    public async Task TokenDeDesenvolvimento_EmDevelopment_ContinuaFuncionando()
    {
        // A rota segue disponivel em dev — e o que mantem o fluxo de teste manual de pe
        var resposta = await _client.PostAsync("/api/auth/token", Corpo(
            """{"usuario":"teste","role":"Admin"}"""));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }

    [Fact]
    public async Task TokenDeDesenvolvimento_ForaDeDevelopment_Retorna404()
    {
        // Emitir JWT sem credencial fora de dev seria porta aberta: a rota some
        using var producao = new VetlyWebApplicationFactory();
        using var cliente = producao
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
            .CreateClient();

        var resposta = await cliente.PostAsync("/api/auth/token", Corpo(
            """{"usuario":"teste","role":"Admin"}"""));

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }
}
