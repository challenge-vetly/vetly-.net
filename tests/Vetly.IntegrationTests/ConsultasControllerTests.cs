using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Vetly.Infrastructure.Data;

namespace Vetly.IntegrationTests;

/// <summary>
/// Testes de integração dos endpoints da API Vetly via WebApplicationFactory.
/// Banco de dados Oracle é substituído por InMemory para isolar o ambiente de CI.
/// </summary>
public class ConsultasControllerTests : IClassFixture<VetlyWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ConsultasControllerTests(VetlyWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Endpoints sem token → 401 ─────────────────────────────────────────────

    [Fact]
    public async Task PostConsultas_SemToken_Retorna401()
    {
        var response = await _client.PostAsync("/api/consultas", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetConsultas_SemToken_Retorna401()
    {
        var response = await _client.GetAsync("/api/consultas");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostIaDiagnostico_SemToken_Retorna401()
    {
        var response = await _client.PostAsync("/api/ia/diagnostico", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Validação de CRMV com token válido → 400 ─────────────────────────────

    [Fact]
    public async Task PostVeterinarios_CrmvInvalido_ComToken_Retorna400()
    {
        var token = GerarTokenJwt();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new StringContent(
            """{"nome":"Dr. Teste","crmv":"ABC-SP","ufAtuacao":"SP","persona":1,"plano":1}""",
            Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/veterinarios", payload);

        // CRMV inválido é rejeitado com 400 (model validation) ou 422 (BusinessRuleException via middleware)
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity,
            $"Esperado 400 ou 422, recebido {(int)response.StatusCode}");
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static string GerarTokenJwt()
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("VetlySecretKey_MustBeAtLeast32CharactersLong!"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "Vetly",
            audience: "VetlyAPI",
            claims: new[] { new Claim(ClaimTypes.NameIdentifier, "test-user") },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// Factory customizada que substitui o Oracle pelo banco InMemory,
/// permitindo rodar os testes de integração sem infraestrutura externa.
/// </summary>
public class VetlyWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove o registro do DbContextOptions<VetlyDbContext> feito com Oracle
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<VetlyDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Substitui por InMemory para isolar CI/CD do banco Oracle
            services.AddDbContext<VetlyDbContext>(options =>
                options.UseInMemoryDatabase("VetlyIntegrationTest_" + Guid.NewGuid()));
        });
    }
}
