using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Vetly.Application.Factories;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Application.Strategies.Cancelamento;
using Vetly.Application.Strategies.Split;
using Vetly.Infrastructure.Data;
using Vetly.Infrastructure.Repositories;
using Vetly.API.Middlewares;
using Vetly.API.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Carrega appsettings.{Environment}.local.json (ex: appsettings.Development.local.json) se existir.
// Esse arquivo é gitignored e deve ser criado localmente com as credenciais de cada ambiente.
builder.Configuration
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.local.json",
        optional: true,
        reloadOnChange: true);

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── OpenAPI / Scalar ─────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// ── Database — Oracle EF Core ────────────────────────────────────────────────
builder.Services.AddDbContext<VetlyDbContext>(options =>
    options.UseOracle(builder.Configuration.GetConnectionString("OracleConnection")));

// ── JWT Bearer Authentication ────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key nao configurada.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApenasAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("VeterinarioOuAdmin", policy => policy.RequireRole("Admin", "Veterinario"));
});

// ── Repositórios ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IVeterinarioRepository, VeterinarioRepository>();
builder.Services.AddScoped<IAnimalRepository, AnimalRepository>();
builder.Services.AddScoped<ITutorRepository, TutorRepository>();
builder.Services.AddScoped<IConsultaRepository, ConsultaRepository>();
builder.Services.AddScoped<IInternacaoRepository, InternacaoRepository>();
builder.Services.AddScoped<IExameRepository, ExameRepository>();
builder.Services.AddScoped<IDocumentoRepository, DocumentoRepository>();
builder.Services.AddScoped<IPagamentoRepository, PagamentoRepository>();
builder.Services.AddScoped<IEmpresaRepository, EmpresaRepository>();
builder.Services.AddScoped<ILembreteRepository, LembreteRepository>();

// ── Serviços de Aplicação ────────────────────────────────────────────────────
builder.Services.AddScoped<IVeterinarioService, VeterinarioService>();
builder.Services.AddScoped<IAnimalService, AnimalService>();
builder.Services.AddScoped<ITutorService, TutorService>();
builder.Services.AddScoped<IConsultaService, ConsultaService>();
builder.Services.AddScoped<IDocumentoService, DocumentoService>();
builder.Services.AddScoped<IInternacaoService, InternacaoService>();
builder.Services.AddScoped<IExameService, ExameService>();
builder.Services.AddScoped<IPagamentoService, PagamentoService>();
builder.Services.AddScoped<IEmpresaService, EmpresaService>();
builder.Services.AddScoped<ILembreteService, LembreteService>();

// ── Factories (IEnumerable<IDocumentoFactory> — resolvidas pelo DI) ──────────
builder.Services.AddScoped<IDocumentoFactory, ProntuarioFactory>();
builder.Services.AddScoped<IDocumentoFactory, ReceitaVeterinariaFactory>();
builder.Services.AddScoped<IDocumentoFactory, AtestadoFactory>();
builder.Services.AddScoped<IDocumentoFactory, NotaFiscalFactory>();

// ── Strategies — Cancelamento (por prioridade) ───────────────────────────────
builder.Services.AddScoped<ICancelamentoStrategy, ReembolsoIntegralStrategy>();
builder.Services.AddScoped<ICancelamentoStrategy, ReembolsoParcialStrategy>();
builder.Services.AddScoped<ICancelamentoStrategy, SemReembolsoStrategy>();

// ── Strategies — Split Financeiro ────────────────────────────────────────────
builder.Services.AddScoped<ISplitFinanceiroStrategy, SplitAutonomoStrategy>();
builder.Services.AddScoped<ISplitFinanceiroStrategy, SplitEmpresaStrategy>();

// ── OllamaService — HttpClient com timeout de 120s ──────────────────────────
builder.Services.AddHttpClient<IOllamaService, OllamaService>(client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int>("Ollama:TimeoutSeconds", 120));
});

// ── Health Checks ────────────────────────────────────────────────────────────
// Registrado depois das dependencias (DbContext e HttpClient do Ollama) para que
// cada check reaproveite exatamente a mesma configuracao usada pela aplicacao.
//
// Tags definem quais checks rodam em cada endpoint:
//   "live"  → a API esta de pe (nao toca dependencia externa nenhuma)
//   "ready" → a API consegue atender de verdade (banco e servicos externos)
builder.Services.AddHealthChecks()

    // Saude da propria API: se o processo responde a esta lambda, o host esta vivo.
    .AddCheck(
        name: "api",
        check: () => HealthCheckResult.Healthy("API no ar."),
        tags: ["live"])

    // Conectividade com o Oracle. AddDbContextCheck executa CanConnectAsync no
    // VetlyDbContext, ou seja, abre conexao de verdade — nao apenas valida a string.
    // Sem banco a API nao entrega nada: Unhealthy (=> HTTP 503 em /health/ready).
    .AddDbContextCheck<VetlyDbContext>(
        name: "oracle-db",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready", "db", "oracle"],
        customTestQuery: async (dbContext, cancellationToken) =>
        {
            // O teste padrao (CanConnectAsync) engole a excecao e devolve apenas
            // false — o relatorio sai sem motivo nenhum da falha. Abrindo a conexao
            // explicitamente, o erro do Oracle (ex.: ORA-01017) sobe e e capturado
            // pelo HealthCheckService, aparecendo no JSON de resposta.
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
            await dbContext.Database.CloseConnectionAsync();
            return true;
        })

    // Disponibilidade do servico externo (Ollama / LLM local).
    // Degraded e nao Unhealthy: sem IA a API segue operando (ver OllamaHealthCheck).
    .AddCheck<OllamaHealthCheck>(
        name: "ollama",
        failureStatus: HealthStatus.Degraded,
        tags: ["ready", "external"]);

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Middlewares ───────────────────────────────────────────────────────────────
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// ── OpenAPI (Scalar) ──────────────────────────────────────────────────────────
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "Vetly API";
    options.Theme = ScalarTheme.DeepSpace;
    options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Endpoints de Health Check ────────────────────────────────────────────────
// Detalhes de excecao so aparecem fora de Producao (ver HealthCheckResponseWriter).
var escritorDeResposta = HealthCheckResponseWriter.Create(
    incluirDetalhesDeErro: !app.Environment.IsProduction());

// Liveness — o processo esta vivo? Roda so o check "api", sem tocar banco nem Ollama.
// Usado por orquestradores para decidir REINICIAR o container.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = escritorDeResposta
});

// Readiness — as dependencias estao prontas? Roda os checks marcados com "ready".
// Usado para decidir se o container recebe TRAFEGO (503 tira de rotacao).
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = escritorDeResposta
});

// Diagnostico completo — todos os checks registrados, sem filtro.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = escritorDeResposta
});

app.Run();
