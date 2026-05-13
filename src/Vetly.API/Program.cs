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

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddAuthorization();

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

app.Run();
