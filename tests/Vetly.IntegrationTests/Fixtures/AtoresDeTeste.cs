using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Vetly.Infrastructure.Data;

namespace Vetly.IntegrationTests;

/// <summary>
/// Quem age nos testes de integração da Sprint 4: Responsável real (registrado e
/// com consentimento), Admin e veterinário por token, e semeadura direta no banco.
///
/// O Responsável passa pelo cadastro de verdade porque o portão de consentimento
/// (RN-060) barra qualquer token de Tutor sem consentimento gravado — um token
/// sintético testaria um caminho que o app nunca percorre. Admin e veterinário vão
/// por token porque nenhum filtro consulta o cadastro deles nas rotas testadas.
/// </summary>
public static class AtoresDeTeste
{
    /// <summary>Serialização das respostas, igual à do app (camelCase).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Media type que liga o HATEOAS.</summary>
    public const string Hal = "application/hal+json";

    /// <summary>Responsável cadastrado, com token e consentimento de atendimento.</summary>
    public sealed record Responsavel(Guid TutorId, string Token);

    /// <summary>Corpo JSON de requisição.</summary>
    public static StringContent Corpo(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>Cadastra um Responsável pelo fluxo real e concede o consentimento (RN-060).</summary>
    public static async Task<Responsavel> CriarResponsavelAsync(HttpClient cliente, string nome = "Responsavel")
    {
        var email = $"{nome.ToLowerInvariant()}-{Guid.NewGuid():N}@exemplo.com";

        var registro = await cliente.PostAsync("/api/auth/registro/tutor", Corpo(
            $$"""
            {"nome":"{{nome}}","email":"{{email}}","telefone":"11999998888","senha":"senha-forte-123"}
            """));
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

        var sessao = await registro.Content.ReadFromJsonAsync<JsonElement>(Json);
        var token = sessao.GetProperty("token").GetString()!;
        var tutorId = sessao.GetProperty("tutorId").GetGuid();

        var consentir = new HttpRequestMessage(HttpMethod.Put, $"/api/tutores/{tutorId}/consentimentos")
        {
            Content = Corpo("""{"consentimentos":[{"finalidade":"Atendimento","concedido":true}]}""")
        };
        consentir.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await cliente.SendAsync(consentir)).StatusCode);

        return new Responsavel(tutorId, token);
    }

    /// <summary>Cadastra um cachorro do Responsável pelo fluxo real e devolve o id.</summary>
    public static async Task<Guid> CriarPetAsync(HttpClient cliente, Responsavel responsavel, string nome = "Thor")
    {
        var resposta = await EnviarAsync(cliente, HttpMethod.Post, "/api/animais", responsavel.Token,
            $$"""
            {"nome":"{{nome}}","especie":"Canino","raca":"SRD",
             "dataNascimento":"2023-04-10T00:00:00Z","tutorId":"{{responsavel.TutorId}}","pesoKg":12.5}
            """);
        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);

        return (await JsonDe(resposta)).GetProperty("id").GetGuid();
    }

    /// <summary>Token de Admin assinado com a chave de teste.</summary>
    public static string TokenAdmin() => Token("Admin");

    /// <summary>Token de veterinário com o id informado.</summary>
    public static string TokenVeterinario(Guid veterinarioId) =>
        Token("Veterinario", new Claim("veterinarioId", veterinarioId.ToString()));

    private static string Token(string role, params Claim[] extras)
    {
        var chave = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("VetlySecretKey_MustBeAtLeast32CharactersLong!"));

        Claim[] claims =
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("role", role),
            .. extras
        ];

        var token = new JwtSecurityToken(
            issuer: "Vetly",
            audience: "VetlyAPI",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(chave, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Grava entidades direto no banco InMemory compartilhado.</summary>
    public static async Task SemearAsync(VetlyWebApplicationFactory factory, params object[] entidades)
    {
        using var escopo = factory.Services.CreateScope();
        var db = escopo.ServiceProvider.GetRequiredService<VetlyDbContext>();
        db.AddRange(entidades);
        await db.SaveChangesAsync();
    }

    /// <summary>Lê o banco num escopo próprio, como uma conferência fora da requisição.</summary>
    public static async Task<T> LerAsync<T>(VetlyWebApplicationFactory factory, Func<VetlyDbContext, Task<T>> leitura)
    {
        using var escopo = factory.Services.CreateScope();
        return await leitura(escopo.ServiceProvider.GetRequiredService<VetlyDbContext>());
    }

    /// <summary>
    /// Requisição autenticada, opcionalmente com Accept e corpo. Fora de GET leva uma
    /// <c>Idempotency-Key</c> nova, a menos que o teste passe a sua.
    /// </summary>
    public static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient cliente, HttpMethod metodo, string rota, string token,
        string? corpo = null, string? accept = null, string? chaveDeIdempotencia = null)
    {
        var requisicao = new HttpRequestMessage(metodo, rota);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (accept is not null)
            requisicao.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

        if (corpo is not null)
            requisicao.Content = Corpo(corpo);

        // Rotas [Idempotente] exigem a chave; uma nova por chamada e o que o app faz
        // para cada acao distinta do usuario
        if (metodo != HttpMethod.Get)
            requisicao.Headers.Add("Idempotency-Key", chaveDeIdempotencia ?? Guid.NewGuid().ToString());

        return await cliente.SendAsync(requisicao);
    }

    /// <summary>Lê o corpo como JSON.</summary>
    public static async Task<JsonElement> JsonDe(HttpResponseMessage resposta) =>
        await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);

    /// <summary>Rels de uma lista de links.</summary>
    public static List<string> Rels(JsonElement links) =>
        links.EnumerateArray().Select(l => l.GetProperty("rel").GetString()!).ToList();
}
