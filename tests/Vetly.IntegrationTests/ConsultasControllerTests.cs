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
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Domain.ValueObjects;
using Vetly.Infrastructure.Data;

namespace Vetly.IntegrationTests;

/// <summary>
/// Testes de integração dos endpoints da API Vetly via WebApplicationFactory.
/// Banco de dados Oracle é substituído por InMemory para isolar o ambiente de CI.
/// </summary>
public class ConsultasControllerTests : IClassFixture<VetlyWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly VetlyWebApplicationFactory _factory;

    public ConsultasControllerTests(VetlyWebApplicationFactory factory)
    {
        _factory = factory;
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

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostVeterinarios_CrmvDuplicado_ComToken_Retorna422()
    {
        const string crmvExistente = "99999-SP";

        // Seed via DI da factory — usa o mesmo InMemoryServiceProvider isolado
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VetlyDbContext>();
            var crmv = new Crmv(crmvExistente);
            var vet = new Veterinario("Dr. Existente", crmv, "SP", PersonaVeterinario.Autonomo, PlanoAssinatura.Profissional);
            await db.Veterinarios.AddAsync(vet);
            await db.SaveChangesAsync();
        }

        var token = GerarTokenJwt();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new StringContent(
            $$$"""{"nome":"Dr. Duplicado","crmv":"{{{crmvExistente}}}","ufAtuacao":"SP","persona":1,"plano":1}""",
            Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/veterinarios", payload);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVeterinarios_ComTokenSemRoleAdmin_Retorna403()
    {
        var token = GerarTokenJwt(role: "Veterinario");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.DeleteAsync($"/api/veterinarios/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Contrato de enums no JSON ────────────────────────────────────────────

    /// <summary>
    /// Enums trafegam como string nos dois sentidos: aceitos na entrada e devolvidos
    /// como string na resposta, em vez do inteiro que o front nao consegue interpretar.
    /// </summary>
    [Fact]
    public async Task PostVeterinarios_ComEnumsComoString_Retorna201ESerializaEnumComoString()
    {
        var token = GerarTokenJwt();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new StringContent(
            """{"nome":"Dra. Enum","crmv":"77777-SP","ufAtuacao":"SP","persona":"Autonomo","plano":"Enterprise"}""",
            Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/veterinarios", payload);
        var corpo = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("\"persona\":\"Autonomo\"", corpo);
        Assert.Contains("\"plano\":\"Enterprise\"", corpo);
    }

    // ── Escopo de acesso (C-07 parcial, RN-069/RN-106) ───────────────────────

    [Fact]
    public async Task GetTutores_ComTokenSemRoleAdmin_Retorna403()
    {
        // A lista completa de Responsaveis e dado pessoal: nao sai para qualquer autenticado
        var token = GerarTokenJwt(role: "Veterinario");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/tutores");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTutores_SemToken_Retorna401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/tutores");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static string GerarTokenJwt(string role = "Admin")
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("VetlySecretKey_MustBeAtLeast32CharactersLong!"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "Vetly",
            audience: "VetlyAPI",
            claims: new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim("role", role)
            },
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
    // Nome fixo por instância de factory — compartilhado entre testes e seeding
    public static readonly string DatabaseName = "VetlyIntegrationTest_" + Guid.NewGuid();

    // ServiceProvider isolado com apenas o provider InMemory — evita conflito com Oracle no DI
    private static readonly IServiceProvider InMemoryServiceProvider =
        new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove o DbContextOptions<VetlyDbContext> configurado com Oracle
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<VetlyDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Substitui por InMemory usando service provider isolado
            // UseInternalServiceProvider evita que o EF Core consulte os serviços Oracle do DI
            services.AddDbContext<VetlyDbContext>(options =>
                options
                    .UseInternalServiceProvider(InMemoryServiceProvider)
                    .UseInMemoryDatabase(DatabaseName));
        });
    }
}
