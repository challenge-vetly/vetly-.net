using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Vetly.Application.Observability;

namespace Vetly.API.Observability;

/// <summary>
/// Reune, num lugar so, a configuracao dos tres pilares de observabilidade da Vetly:
/// <b>logs</b> estruturados (Serilog), <b>traces</b> distribuidos e <b>metricas</b>
/// (OpenTelemetry).
/// </summary>
/// <remarks>
/// <para>
/// Os tres respondem a perguntas diferentes e por isso nao se substituem. A metrica
/// responde <i>"esta ruim?"</i> — e barata, agregada, e o que dispara o alerta. O trace
/// responde <i>"ruim onde?"</i> — mostra a requisicao inteira quebrada em spans, e
/// aponta a camada culpada. O log responde <i>"ruim por que?"</i> — traz o detalhe do
/// caso concreto. Ter so um dos tres significa, na pratica, descobrir o incidente pelo
/// cliente, ou saber que ele existe sem conseguir explicar.
/// </para>
/// <para>
/// A costura entre eles e o <c>TraceId</c>: o Serilog carimba toda linha com ele (ver
/// <c>CorrelationIdMiddleware</c>), o OpenTelemetry exporta o span com o mesmo valor, e
/// o <c>ProblemDetails</c> devolve esse valor ao cliente. Um chamado de suporte que
/// comeca com um id termina no span exato.
/// </para>
/// </remarks>
public static class ConfiguracaoDeObservabilidade
{
    /// <summary>
    /// Configura o Serilog como unico provedor de log da aplicacao.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A configuracao vem do <c>appsettings.json</c> (secao <c>Serilog</c>), e nao do
    /// codigo, de proposito: mudar o nivel de log de um namespace em producao nao pode
    /// exigir recompilar e redeployar. O que fica em codigo sao os enriquecedores, que
    /// sao decisao de arquitetura e nao de ambiente.
    /// </para>
    /// <para>
    /// <c>Enrich.FromLogContext()</c> e a linha mais importante: sem ela, as
    /// propriedades empilhadas por <c>LogContext.PushProperty</c> — <c>CorrelationId</c>
    /// e <c>TraceId</c> — simplesmente nao aparecem no log, e a correlacao inteira deixa
    /// de existir.
    /// </para>
    /// </remarks>
    /// <param name="builder">Builder da aplicacao web.</param>
    /// <returns>O mesmo builder, para encadeamento.</returns>
    public static WebApplicationBuilder AddLogEstruturado(this WebApplicationBuilder builder)
    {
        // O logger e construido aqui e atribuido ao Log.Logger estatico, em vez de
        // delegado ao UseSerilog. A diferenca importa: o UseSerilog "congela" o logger
        // de bootstrap quando o container e construido, e um mesmo processo que sobe
        // mais de um host — exatamente o que a WebApplicationFactory faz nos testes de
        // integracao — tentaria congelar o mesmo logger duas vezes e falharia com
        // "the logger is already frozen". Construindo direto, cada host tem o seu, e o
        // Program.cs continua podendo usar Log.Fatal fora do escopo do container.
        Log.Logger = new LoggerConfiguration()
            // Niveis, sinks e overrides por namespace: tudo em appsettings.
            .ReadFrom.Configuration(builder.Configuration)

            // Sem isto, CorrelationId e TraceId nao chegam ao log.
            .Enrich.FromLogContext()

            // Identificacao da origem — essencial assim que ha mais de uma instancia:
            // "o erro so acontece numa das maquinas" e uma pergunta que so se responde
            // com o log dizendo qual maquina.
            .Enrich.WithProperty("Aplicacao", VetlyTelemetry.NomeDoServico)
            .Enrich.WithProperty("Versao", VetlyTelemetry.Versao)
            .Enrich.WithProperty("Ambiente", builder.Environment.EnvironmentName)
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .CreateLogger();

        // Registra o mesmo logger como provedor de ILogger<T>: todo servico da
        // aplicacao continua recebendo a abstracao da Microsoft, sem saber que ha um
        // Serilog do outro lado. Trocar de biblioteca de log e trocar estas linhas.
        builder.Services.AddSerilog(Log.Logger);

        return builder;
    }

    /// <summary>
    /// Registra o <c>UseSerilogRequestLogging</c> com o resumo de requisicao enriquecido.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uma linha por requisicao, com o template preservado — <c>{RequestPath}</c> e
    /// <c>{Elapsed}</c> viram <b>campos</b> no log estruturado, nao texto concatenado.
    /// E essa a diferenca entre "consigo procurar por todas as requisicoes acima de 2s"
    /// e "tenho um txt com frases".
    /// </para>
    /// <para>
    /// Sondas de saude entram em <c>Verbose</c>: um <c>/health/live</c> a cada 10s vira
    /// 8.640 linhas por dia e por instancia, e afogaria o log util. Elas nao somem —
    /// baixar o nivel de log da secao <c>Serilog</c> as traz de volta quando se quer
    /// depurar o proprio probe.
    /// </para>
    /// </remarks>
    /// <param name="app">Aplicacao ja construida.</param>
    /// <returns>A mesma aplicacao, para encadeamento.</returns>
    public static WebApplication UseLogDeRequisicoes(this WebApplication app)
    {
        app.UseSerilogRequestLogging(opcoes =>
        {
            opcoes.MessageTemplate =
                "{RequestMethod} {RequestPath} respondeu {StatusCode} em {Elapsed:0.0000} ms";

            opcoes.GetLevel = (contexto, _, excecao) =>
            {
                // Excecao vazando ate aqui e sempre erro, qualquer que seja a rota.
                if (excecao is not null)
                    return LogEventLevel.Error;

                if (contexto.Response.StatusCode >= 500)
                    return LogEventLevel.Error;

                // 4xx e o cliente errando, nao o servidor: Warning, para nao acionar
                // alerta de disponibilidade por um payload invalido.
                if (contexto.Response.StatusCode >= 400)
                    return LogEventLevel.Warning;

                return EhSonda(contexto.Request.Path)
                    ? LogEventLevel.Verbose
                    : LogEventLevel.Information;
            };

            // Campos que so existem no fim da requisicao — nao ha como o template
            // captura-los sozinho.
            opcoes.EnrichDiagnosticContext = (diagnostico, contexto) =>
            {
                diagnostico.Set("Host", contexto.Request.Host.Value);
                diagnostico.Set("Protocolo", contexto.Request.Protocol);
                diagnostico.Set("CorrelationId", contexto.TraceIdentifier);

                // Quem fez a chamada. E o que transforma "alguem apagou o horario" em
                // "este usuario apagou o horario".
                if (contexto.User.Identity?.IsAuthenticated == true)
                {
                    diagnostico.Set("Usuario", contexto.User.Identity.Name ?? "(sem nome)");
                    diagnostico.Set("Perfil", contexto.User.FindFirst("role")?.Value ?? "(sem role)");
                }
            };
        });

        return app;
    }

    /// <summary>
    /// Configura traces e metricas do OpenTelemetry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Traces</b> cobrem as tres fronteiras que uma requisicao da Vetly atravessa:
    /// a borda HTTP (ASP.NET Core), o banco (EF Core/Oracle) e as chamadas de saida
    /// (<c>HttpClient</c>, onde vive o Ollama). Somados aos spans de dominio da
    /// <see cref="VetlyTelemetry.Fonte"/>, produzem a cadeia completa
    /// <c>controller → servico → repositorio → SQL</c> em um unico trace.
    /// </para>
    /// <para>
    /// <b>Metricas</b> reunem o que a plataforma ja publica (duracao de requisicao,
    /// conexoes do Kestrel, GC e thread pool do runtime) com o que so a Vetly sabe
    /// (checkout, split, decisao sobre rascunho da IA). O exportador Prometheus expoe
    /// tudo em <c>/metrics</c>.
    /// </para>
    /// <para>
    /// O exportador OTLP so e ligado quando ha endpoint configurado. Ligado sem coletor
    /// do outro lado, ele tentaria exportar em lote a cada poucos segundos e encheria o
    /// log de falhas de conexao — barulho que faz a observabilidade parecer o problema.
    /// </para>
    /// </remarks>
    /// <param name="builder">Builder da aplicacao web.</param>
    /// <returns>O mesmo builder, para encadeamento.</returns>
    public static WebApplicationBuilder AddTracingEMetricas(this WebApplicationBuilder builder)
    {
        var configuracao = builder.Configuration;
        var ambiente = builder.Environment;

        var endpointOtlp = configuracao["OpenTelemetry:Otlp:Endpoint"];
        var exportarParaConsole = configuracao.GetValue("OpenTelemetry:ExportarParaConsole", false);

        builder.Services.AddOpenTelemetry()

            // Identidade comum a traces e metricas: e o que separa a Vetly dos demais
            // servicos no mesmo backend, e uma instancia da outra.
            .ConfigureResource(recurso => recurso
                .AddService(
                    serviceName: configuracao["OpenTelemetry:ServiceName"] ?? VetlyTelemetry.NomeDoServico,
                    serviceVersion: VetlyTelemetry.Versao,
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes(
                [
                    new KeyValuePair<string, object>("deployment.environment", ambiente.EnvironmentName)
                ]))

            .WithTracing(tracing =>
            {
                tracing
                    // Spans de dominio (consulta.checkout, pagamento.webhook, ...).
                    // Fonte nao registrada e fonte descartada.
                    .AddSource(VetlyTelemetry.NomeDaFonte)

                    .AddAspNetCoreInstrumentation(opcoes =>
                    {
                        // Anexa a excecao ao span: sem isto, o trace mostra um span
                        // vermelho sem dizer o que estourou.
                        opcoes.RecordException = true;

                        // Sonda de saude nao vira trace. Sao 99% do volume e 0% do valor.
                        opcoes.Filter = contexto => !EhSonda(contexto.Request.Path);
                    })

                    // Chamadas de saida — o Ollama e o Node-RED aparecem como spans
                    // filhos, com a latencia deles separada da nossa.
                    .AddHttpClientInstrumentation()

                    // Cada consulta ao Oracle vira um span filho. E o que responde
                    // "a rota esta lenta por causa do banco ou da regra?".
                    .AddEntityFrameworkCoreInstrumentation(opcoes =>
                    {
                        // Nesta versao da instrumentacao, o texto da consulta e os
                        // valores dos parametros deixaram de ser controlados por
                        // propriedade e passaram a depender do opt-in de convencao
                        // semantica do OpenTelemetry (OTEL_SEMCONV_STABILITY_OPT_IN).
                        // O padrao — sem valores de parametro — e exatamente o que a
                        // LGPD pede aqui: parametro de consulta carrega nome de
                        // Responsavel, id de animal e conteudo clinico (§7.2), e isso
                        // nao pode sair para um backend de tracing de terceiro.
                        //
                        // O que se acrescenta e o timeout efetivo do comando: span de
                        // banco lento sem essa informacao nao distingue "consulta
                        // pesada" de "conexao esperando o limite estourar".
                        opcoes.EnrichWithIDbCommand = (atividade, comando) =>
                            atividade.SetTag("vetly.db.timeout_s", comando.CommandTimeout);
                    });

                if (exportarParaConsole)
                    tracing.AddConsoleExporter();

                if (!string.IsNullOrWhiteSpace(endpointOtlp))
                    tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(endpointOtlp));
            })

            .WithMetrics(metricas =>
            {
                metricas
                    // Medidores da casa.
                    .AddMeter(VetlyTelemetry.NomeDoMedidor)
                    .AddMeter(MetricasHttp.NomeDoMedidor)

                    // Medidores da plataforma: duracao de requisicao, fila e conexoes do
                    // Kestrel, pool de conexoes HTTP de saida.
                    .AddMeter("Microsoft.AspNetCore.Hosting")
                    .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                    .AddMeter("System.Net.Http")

                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()

                    // GC, heap, thread pool, excecoes. E o primeiro lugar a olhar quando
                    // a latencia sobe sem que o banco tenha piorado.
                    .AddRuntimeInstrumentation()

                    // Expoe /metrics no formato de texto do Prometheus.
                    .AddPrometheusExporter();

                if (exportarParaConsole)
                    metricas.AddConsoleExporter();

                if (!string.IsNullOrWhiteSpace(endpointOtlp))
                    metricas.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(endpointOtlp));
            });

        return builder;
    }

    /// <summary>
    /// Diz se o path e sonda de infraestrutura — health check ou raspagem de metricas.
    /// </summary>
    /// <param name="caminho">Path da requisicao.</param>
    /// <returns><c>true</c> quando a requisicao e de infraestrutura, nao de negocio.</returns>
    private static bool EhSonda(PathString caminho) =>
        caminho.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
        || caminho.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase);
}
