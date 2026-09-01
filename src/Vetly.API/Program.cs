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
using Vetly.Application.Strategies.Split;
using Vetly.Infrastructure.Adapters;
using Vetly.Infrastructure.Data;
using Vetly.Infrastructure.Jobs;
using Vetly.Infrastructure.Repositories;
using Vetly.Infrastructure.Security;
using Vetly.API.Middlewares;
using Vetly.API.HealthChecks;
using Vetly.API.Filters;
using Vetly.API.Jobs;
using Vetly.API.Security;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vetly.API.Observability;
using Serilog;

// ── Logger de bootstrap ───────────────────────────────────────────────────────
// Existe para cobrir a janela entre o inicio do processo e o Serilog definitivo, que
// so nasce depois de o appsettings ser lido. Sem ele, um erro de configuracao (uma
// connection string ausente, a Jwt:Key faltando) derruba a API com um stacktrace cru
// no console — justamente o tipo de falha que se investiga pelo log.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Carrega appsettings.{Environment}.local.json (ex: appsettings.Development.local.json) se existir.
// Esse arquivo é gitignored e deve ser criado localmente com as credenciais de cada ambiente.
builder.Configuration
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.local.json",
        optional: true,
        reloadOnChange: true);

// ── Observabilidade (§ Monitoramento) ─────────────────────────────────────────
// Registrada antes de tudo de proposito: falha na composicao do container e um erro
// de startup, e sem log configurado ele sai como texto cru no console e some.
//
//   AddLogEstruturado   → Serilog como unico provedor de log, lendo appsettings
//   AddTracingEMetricas → OpenTelemetry: traces (ASP.NET Core, EF Core, HttpClient e
//                         spans de dominio) e metricas (plataforma, runtime e negocio)
builder.AddLogEstruturado();
builder.AddTracingEMetricas();

// ── Controllers ───────────────────────────────────────────────────────────────
builder.Services
    .AddControllers(options =>
    {
        // Portao de consentimento (RN-060): roda em tudo e falha fechado. A rota que
        // precisa funcionar antes do consentimento se declara [IsentoDeConsentimento].
        options.Filters.Add<ConsentimentoAtendimentoFilter>();

        // Encerramento de acesso do vet desativado (RN-022), preservando so o que a
        // RN-024 garante a ele. Tambem falha fechado.
        options.Filters.Add<VetDesativadoFilter>();

        // Idempotencia (§2.5): so age nas rotas marcadas com [Idempotente] — o que
        // nao pode acontecer duas vezes por um reenvio do app.
        options.Filters.Add<IdempotencyFilter>();
    })
    .AddJsonOptions(options =>
    {
        // Enums trafegam como STRING no JSON ("Presencial", "Confirmado"), nao como numero.
        // O contrato numerico e ilegivel para o front e quebra a cada reordenacao do enum.
        // A persistencia nao muda: o EF Core continua gravando NUMBER pelo valor do enum.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

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

    // Vet desativado nao entra aqui: a role dele e VetDesativado, e o acesso permitido
    // se limita ao extrato dos proprios atendimentos (RN-022/RN-024).

    // Rotas do app do Responsavel (RN-060 em diante)
    options.AddPolicy("ApenasTutor", policy => policy.RequireRole("Tutor"));

    // Rotas em que o Responsavel opera os proprios dados e o Admin opera os de todos.
    // A posse por linha e conferida no serviço, com a claim tutorId (RN-105/RN-106).
    options.AddPolicy("TutorOuAdmin", policy => policy.RequireRole("Tutor", "Admin"));
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
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IDispositivoRepository, DispositivoRepository>();
builder.Services.AddScoped<IAgendaRepository, AgendaRepository>();
builder.Services.AddScoped<IBuscaRepository, BuscaRepository>();
builder.Services.AddScoped<IListaEsperaRepository, ListaEsperaRepository>();
builder.Services.AddScoped<IFilaDeJobs, FilaDeJobs>();
builder.Services.AddScoped<IMidiaRepository, MidiaRepository>();
builder.Services.AddScoped<ICapturaRepository, CapturaRepository>();
builder.Services.AddScoped<IAuditoriaIaRepository, AuditoriaIaRepository>();
builder.Services.AddScoped<IColmeiaRepository, ColmeiaRepository>();
builder.Services.AddScoped<IObrigacaoRepository, ObrigacaoRepository>();
builder.Services.AddScoped<IFidelidadeRepository, FidelidadeRepository>();
builder.Services.AddScoped<IAvaliacaoRepository, AvaliacaoRepository>();
builder.Services.AddScoped<INotificacaoRepository, NotificacaoRepository>();
builder.Services.AddSingleton<IGeradorDePdf, GeradorDePdfSimples>();

// Assinatura de documentos (RN-087): no MVP, o nome digitado pelo profissional.
// Em producao, certificado ICP-Brasil vinculado ao CRMV — troca-se a implementacao
// desta porta sem mexer no fluxo em volta.
var adaptadorAssinatura = builder.Configuration["Adaptadores:Assinatura"] ?? "NomeDigitado";
builder.Services.AddScoped<IAssinaturaAdapter>(sp => adaptadorAssinatura switch
{
    "NomeDigitado" => new AssinaturaAdapterNomeDigitado(
        sp.GetRequiredService<ILogger<AssinaturaAdapterNomeDigitado>>()),

    _ => throw new InvalidOperationException(
        $"Adaptador de assinatura '{adaptadorAssinatura}' nao reconhecido. Valor valido: NomeDigitado.")
});

// ── Escopo do usuário da requisição (RN-105/RN-106) ──────────────────────────
// Os serviços leem identidade e escopo daqui, nunca de parametro vindo do cliente.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUsuarioAtual, UsuarioAtual>();

// ── Segurança: hash de senha e emissão de token ──────────────────────────────
// PBKDF2-HMAC-SHA256 com os parâmetros do OWASP; ver Pbkdf2SenhaHasher.
builder.Services.AddSingleton<ISenhaHasher, Pbkdf2SenhaHasher>();
builder.Services.AddSingleton<IGeradorDeTokenJwt, GeradorDeTokenJwt>();
builder.Services.AddSingleton<IGeradorDeSenhaTemporaria, GeradorDeSenhaTemporaria>();
builder.Services.AddScoped<ITokenDeServico, TokenDeServico>();

// ── Adaptadores de dependência externa (C2) ──────────────────────────────────
// Trocar de fornecedor = trocar o registro aqui, sem tocar em serviço nenhum (§5).
// "Simulado" e o padrao no MVP: a API e real, a dependencia externa e que e simulada.
var adaptadorCrmv = builder.Configuration["Adaptadores:Crmv"] ?? "Simulado";
switch (adaptadorCrmv)
{
    case "Simulado":
        builder.Services.AddScoped<ICrmvAdapter, CrmvAdapterSimulado>();
        break;
    default:
        throw new InvalidOperationException(
            $"Adaptador de CRMV '{adaptadorCrmv}' nao reconhecido. Valores validos: Simulado.");
}

// Storage de objetos (§2.6): disco local em desenvolvimento, bucket S3-compativel em
// producao. A API nunca proxia os bytes em nenhum dos dois.
var adaptadorStorage = builder.Configuration["Adaptadores:Storage"] ?? "Local";
switch (adaptadorStorage)
{
    case "Local":
        builder.Services.AddScoped<IStorageAdapter, StorageAdapterLocal>();
        break;
    default:
        throw new InvalidOperationException(
            $"Adaptador de storage '{adaptadorStorage}' nao reconhecido. Valores validos: Local.");
}

// Transcricao de fala (§5.3): Node-RED em producao, simulado em desenvolvimento.
// O contrato do callback e da Vetly nos dois casos.
var adaptadorStt = builder.Configuration["Adaptadores:Stt"] ?? "Simulado";
switch (adaptadorStt)
{
    case "Simulado":
        builder.Services.AddScoped<ISttAdapter, SttAdapterSimulado>();
        break;

    case "NodeRed":
        builder.Services.AddHttpClient<ISttAdapter, SttAdapterNodeRed>(client =>
        {
            client.BaseAddress = new Uri(
                builder.Configuration["NodeRed:BaseUrl"]
                ?? throw new InvalidOperationException("NodeRed:BaseUrl nao configurada."));

            client.Timeout = TimeSpan.FromSeconds(30);
        });
        break;

    default:
        throw new InvalidOperationException(
            $"Adaptador de STT '{adaptadorStt}' nao reconhecido. Valores validos: Simulado, NodeRed.");
}

// Push (RN-092): no MVP, simulado. Trocar por APNs ou FCM e trocar o registro desta
// porta — a diferenca entre provedores nao chega ao servico de notificacoes.
var adaptadorPush = builder.Configuration["Adaptadores:Push"] ?? "Simulado";
builder.Services.AddScoped<IPushAdapter>(sp => adaptadorPush switch
{
    "Simulado" => new PushAdapterSimulado(sp.GetRequiredService<ILogger<PushAdapterSimulado>>()),

    _ => throw new InvalidOperationException(
        $"Adaptador de push '{adaptadorPush}' nao reconhecido. Valor valido: Simulado.")
});

var adaptadorPagamento = builder.Configuration["Adaptadores:Pagamento"] ?? "Simulado";
switch (adaptadorPagamento)
{
    case "Simulado":
        builder.Services.AddScoped<IPagamentoAdapter, PagamentoAdapterSimulado>();
        break;
    default:
        throw new InvalidOperationException(
            $"Adaptador de pagamento '{adaptadorPagamento}' nao reconhecido. Valores validos: Simulado.");
}

var adaptadorGeo = builder.Configuration["Adaptadores:Geocodificacao"] ?? "Simulado";
switch (adaptadorGeo)
{
    case "Simulado":
        builder.Services.AddScoped<IGeocodificacaoAdapter, GeocodificacaoAdapterSimulado>();
        break;
    default:
        throw new InvalidOperationException(
            $"Adaptador de geocodificacao '{adaptadorGeo}' nao reconhecido. Valores validos: Simulado.");
}

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
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDispositivoService, DispositivoService>();
builder.Services.AddScoped<IAgendaService, AgendaService>();
builder.Services.AddScoped<IBuscaService, BuscaService>();
builder.Services.AddScoped<IMidiaService, MidiaService>();
builder.Services.AddScoped<ICapturaService, CapturaService>();
builder.Services.AddScoped<IRascunhoService, RascunhoService>();
builder.Services.AddScoped<IProntuarioService, ProntuarioService>();
builder.Services.AddScoped<IColmeiaService, ColmeiaService>();
builder.Services.AddScoped<IObrigacaoService, ObrigacaoService>();
builder.Services.AddScoped<IFidelidadeService, FidelidadeService>();
builder.Services.AddScoped<IAvaliacaoService, AvaliacaoService>();
builder.Services.AddScoped<INotificacaoService, NotificacaoService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IFinanceiroService, FinanceiroService>();
builder.Services.AddScoped<IRedistribuicaoService, RedistribuicaoService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IListaEsperaService, ListaEsperaService>();

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
// Take rate por plano (RN-070): a maior comissao pertence ao menor plano
builder.Services.AddScoped<ISplitFinanceiroStrategy, SplitBasicoStrategy>();
builder.Services.AddScoped<ISplitFinanceiroStrategy, SplitProfissionalStrategy>();
builder.Services.AddScoped<ISplitFinanceiroStrategy, SplitEnterpriseStrategy>();

// ── OllamaService — HttpClient com timeout de 120s ──────────────────────────
builder.Services.AddHttpClient<IOllamaService, OllamaService>(client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int>("Ollama:TimeoutSeconds", 120));
});

// ── Worker de negocio (§11) ──────────────────────────────────────────────────
// Um BackgroundService no mesmo host, sobre o Oracle que ja existe: nenhum broker
// novo. Handlers e rotinas entram por DI — o worker nao conhece nenhum deles.
builder.Services.AddScoped<IJobHandler, PromoverListaEsperaHandler>();
builder.Services.AddScoped<IJobHandler, ConfirmarPagamentoSimuladoHandler>();
builder.Services.AddScoped<IJobHandler, TranscreverSegmentoHandler>();
builder.Services.AddScoped<IJobHandler, TranscreverSegmentoSimuladoHandler>();
builder.Services.AddScoped<IJobHandler, EstruturarConsultaHandler>();
builder.Services.AddScoped<IJobHandler, CreditarPontosHandler>();

// §4.2: sem esta varredura, motor que aceita o despacho e morre calado deixa a sessao
// presa em AguardandoTranscricao para sempre — e o app, que faz polling do rascunho,
// nunca chega a um estado terminal.
builder.Services.AddScoped<IJobHandler, VerificarTranscricaoTravadaHandler>();

builder.Services.AddScoped<IRotinaPeriodica, VarrerTranscricoesTravadas>();
builder.Services.AddScoped<IRotinaPeriodica, ExpirarLocksDeCheckout>();
builder.Services.AddScoped<IRotinaPeriodica, LimparIdempotenciaVencida>();
builder.Services.AddScoped<IRotinaPeriodica, ExpirarPontosVencidos>();
builder.Services.AddScoped<IRotinaPeriodica, EnviarNotificacoesPendentes>();
builder.Services.AddScoped<IRotinaPeriodica, AvisarObrigacoesVencendo>();

// RN-094/RN-095: sem esta rotina a regua nascia e parava — o alerta a clinica, que
// depende de tres tentativas sem resposta, nunca chegava a disparar.
builder.Services.AddScoped<IRotinaPeriodica, AgendarTentativasDaRegua>();

builder.Services.AddHostedService<VetlyBackgroundService>();

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
// A ordem aqui nao e estetica; cada posicao resolve um problema:
//
//   1. CorrelationId  — precisa ser o primeiro: e ele que define o TraceIdentifier que
//                       todos os outros (inclusive o ProblemDetails de erro) vao usar.
//   2. Log de request — por fora do tratador de excecao, para registrar o status FINAL
//                       da resposta, e nao o caminho que estourou no meio.
//   3. Metricas HTTP  — mesma razao: mede o tempo que o cliente esperou de verdade,
//                       incluindo o custo de montar a resposta de erro.
//   4. Excecoes       — o mais interno dos quatro: converte excecao em ProblemDetails.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseLogDeRequisicoes();
app.UseMiddleware<MetricasHttpMiddleware>();
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

// ── Endpoint de metricas (Prometheus) ────────────────────────────────────────
// Formato de texto do Prometheus, o mesmo que Grafana, Datadog e qualquer coletor
// compativel sabem raspar. Publica em uma unica resposta as tres familias:
//
//   • plataforma → http.server.request.duration, kestrel.active_connections, ...
//   • runtime    → process.runtime.dotnet.gc.*, thread pool, excecoes
//   • negocio    → vetly.checkouts.iniciados, vetly.regras.violadas, vetly.http.*
//
// A rota fica publica, como os health checks: em producao ela seria exposta apenas na
// rede interna, no nivel do ingress — metrica de negocio agregada nao carrega dado
// pessoal, mas revela volume de operacao, que e informacao comercial.
app.MapPrometheusScrapingEndpoint("/metrics");

// ── Execucao ─────────────────────────────────────────────────────────────────
// O try/finally aqui nao e defensivo por moda: sem o CloseAndFlush, o sink de arquivo
// pode perder o ultimo lote em buffer — que e exatamente o lote que contem o motivo
// de a aplicacao ter caido.
try
{
    Log.Information("Vetly API subindo no ambiente {Ambiente}.", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception excecao)
{
    Log.Fatal(excecao, "A Vetly API encerrou de forma inesperada.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
