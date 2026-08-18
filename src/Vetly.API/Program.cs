using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Vetly.Application.Factories;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Application.Strategies.Cancelamento;
using Vetly.Application.Strategies.Comissao;
using Vetly.Application.Strategies.Fidelidade;
using Vetly.Application.Strategies.Split;
using Vetly.Infrastructure.Data;
using Vetly.Infrastructure.Repositories;
using Vetly.API.Middlewares;
using Vetly.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Carrega appsettings.{Environment}.local.json (ex: appsettings.Development.local.json) se existir.
// Esse arquivo é gitignored e deve ser criado localmente com as credenciais de cada ambiente.
builder.Configuration
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.local.json",
        optional: true,
        reloadOnChange: true);

// ── Controllers ───────────────────────────────────────────────────────────────
// Enums trafegam como string no JSON (ex: "finalidade": "CompartilhamentoRede") —
// contrato exigido pelos payloads da spec v2 (ver README de contratos, Fase 13).
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ── Infraestrutura transversal (relógio testável + identidade do usuário atual) ─
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// -- Health Checks -------------------------------------------------------------
builder.Services.AddHealthChecks();

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
builder.Services.AddScoped<IResponsavelRepository, ResponsavelRepository>();
builder.Services.AddScoped<IConsultaRepository, ConsultaRepository>();
builder.Services.AddScoped<IInternacaoRepository, InternacaoRepository>();
builder.Services.AddScoped<IExameRepository, ExameRepository>();
builder.Services.AddScoped<IDocumentoRepository, DocumentoRepository>();
builder.Services.AddScoped<IPagamentoRepository, PagamentoRepository>();
builder.Services.AddScoped<IEmpresaRepository, EmpresaRepository>();
builder.Services.AddScoped<ILembreteRepository, LembreteRepository>();
builder.Services.AddScoped<IConsentimentoLgpdRepository, ConsentimentoLgpdRepository>();
builder.Services.AddScoped<IRegistroOcultadoRepository, RegistroOcultadoRepository>();
builder.Services.AddScoped<ILogAuditoriaIARepository, LogAuditoriaIARepository>();
builder.Services.AddScoped<IConcessaoAcessoProntuarioRepository, ConcessaoAcessoProntuarioRepository>();
builder.Services.AddScoped<ILogAcessoProntuarioRepository, LogAcessoProntuarioRepository>();
builder.Services.AddScoped<IAvaliacaoRepository, AvaliacaoRepository>();
builder.Services.AddScoped<IObrigacaoDoPetRepository, ObrigacaoDoPetRepository>();
builder.Services.AddScoped<IPontosFidelidadeRepository, PontosFidelidadeRepository>();

// ── Serviços de Aplicação ────────────────────────────────────────────────────
builder.Services.AddScoped<IVeterinarioService, VeterinarioService>();
builder.Services.AddScoped<IAnimalService, AnimalService>();
builder.Services.AddScoped<IResponsavelService, ResponsavelService>();
builder.Services.AddScoped<IConsultaService, ConsultaService>();
builder.Services.AddScoped<IDocumentoService, DocumentoService>();
builder.Services.AddScoped<IInternacaoService, InternacaoService>();
builder.Services.AddScoped<IExameService, ExameService>();
builder.Services.AddScoped<IPagamentoService, PagamentoService>();
builder.Services.AddScoped<IEmpresaService, EmpresaService>();
builder.Services.AddScoped<ILembreteService, LembreteService>();
builder.Services.AddScoped<IConsultaIaService, ConsultaIaService>();
builder.Services.AddScoped<IAcessoProntuarioService, AcessoProntuarioService>();
builder.Services.AddScoped<IAvaliacaoService, AvaliacaoService>();
builder.Services.AddScoped<IObrigacaoService, ObrigacaoService>();
builder.Services.AddScoped<IFidelidadeService, FidelidadeService>();

// ── Factories (IEnumerable<IDocumentoFactory> — resolvidas pelo DI) ──────────
builder.Services.AddScoped<IDocumentoFactory, ProntuarioFactory>();
builder.Services.AddScoped<IDocumentoFactory, ReceitaVeterinariaFactory>();
builder.Services.AddScoped<IDocumentoFactory, AtestadoFactory>();
builder.Services.AddScoped<IDocumentoFactory, NotaFiscalFactory>();

// ── Factories — Obrigacoes do pet por especie (RN-069) — generica por ultimo,
// e sempre aplicavel como fallback ────────────────────────────────────────────
builder.Services.AddScoped<IObrigacaoFactory, ObrigacaoCaninaFactory>();
builder.Services.AddScoped<IObrigacaoFactory, ObrigacaoFelinaFactory>();
builder.Services.AddScoped<IObrigacaoFactory, ObrigacaoGenericaFactory>();

// ── Strategies — Cancelamento (por prioridade) ───────────────────────────────
builder.Services.AddScoped<ICancelamentoStrategy, ReembolsoIntegralStrategy>();
builder.Services.AddScoped<ICancelamentoStrategy, ReembolsoParcialStrategy>();
builder.Services.AddScoped<ICancelamentoStrategy, SemReembolsoStrategy>();

// ── Strategies — Split Financeiro ────────────────────────────────────────────
builder.Services.AddScoped<ISplitFinanceiroStrategy, SplitAutonomoStrategy>();
builder.Services.AddScoped<ISplitFinanceiroStrategy, SplitEmpresaStrategy>();

// ── Strategies — Comissao por plano (RN-089) ─────────────────────────────────
builder.Services.AddScoped<IComissaoStrategy, ComissaoBasicoStrategy>();
builder.Services.AddScoped<IComissaoStrategy, ComissaoProfissionalStrategy>();
builder.Services.AddScoped<IComissaoStrategy, ComissaoEnterpriseStrategy>();

// ── Strategies — Desconto de fidelidade por tier (RN-071/072) ───────────────
builder.Services.AddScoped<IDescontoFidelidadeStrategy, DescontoBronzeStrategy>();
builder.Services.AddScoped<IDescontoFidelidadeStrategy, DescontoPrataStrategy>();
builder.Services.AddScoped<IDescontoFidelidadeStrategy, DescontoOuroStrategy>();

// ── OllamaService — HttpClient com timeout de 120s ──────────────────────────
builder.Services.AddHttpClient<IOllamaService, OllamaService>(client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int>("Ollama:TimeoutSeconds", 120));
});

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
app.MapHealthChecks("/health");

app.Run();
