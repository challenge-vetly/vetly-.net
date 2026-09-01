using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vetly.Application.Interfaces;
using Vetly.Application.Observability;

namespace Vetly.API.Jobs;

/// <summary>
/// Worker de negócio da Vetly, hospedado no mesmo processo da API (§11).
///
/// Faz duas coisas a cada ciclo:
/// <list type="number">
///   <item><description>roda as <b>rotinas periódicas</b> vencidas — varreduras que
///   seriam absurdas de modelar como um job por item (expirar locks, limpar
///   idempotência);</description></item>
///   <item><description>executa os <b>jobs</b> da fila que já podem rodar — trabalhos
///   pontuais, cada um com seu alvo (promover a lista de espera de um horário,
///   entregar um webhook simulado).</description></item>
/// </list>
///
/// Polling a cada 30 segundos, que é folgado para janelas de 10 e 15 minutos.
/// Cada ciclo abre o próprio escopo de DI: os serviços são <c>Scoped</c> e não podem
/// ser capturados por um singleton.
///
/// Vive na camada de API porque é ela que hospeda — os handlers e as rotinas, que são
/// o que de fato faz o trabalho, ficam na Infrastructure.
/// </summary>
public class VetlyBackgroundService : BackgroundService
{
    /// <summary>Intervalo entre ciclos.</summary>
    public static readonly TimeSpan IntervaloDoCiclo = TimeSpan.FromSeconds(30);

    /// <summary>Jobs processados por ciclo, para não segurar o worker num lote gigante.</summary>
    private const int JobsPorCiclo = 20;

    private readonly IServiceScopeFactory _escopos;
    private readonly ILogger<VetlyBackgroundService> _logger;

    /// <summary>Última execução de cada rotina periódica, por nome.</summary>
    private readonly Dictionary<string, DateTime> _ultimaExecucao = [];

    public VetlyBackgroundService(IServiceScopeFactory escopos, ILogger<VetlyBackgroundService> logger)
    {
        _escopos = escopos;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "VetlyBackgroundService iniciado. Ciclo a cada {Segundos}s.", IntervaloDoCiclo.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecutarCicloAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Um ciclo com problema nao pode derrubar o worker: sem ele, locks nunca
                // expiram e ninguem na fila de espera e avisado.
                _logger.LogError(ex, "Falha no ciclo do VetlyBackgroundService. O proximo ciclo segue normalmente.");
            }

            try
            {
                await Task.Delay(IntervaloDoCiclo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("VetlyBackgroundService encerrado.");
    }

    /// <summary>Um ciclo completo: rotinas periódicas e depois a fila.</summary>
    internal async Task ExecutarCicloAsync(CancellationToken cancellationToken)
    {
        // O worker roda fora de qualquer requisicao HTTP: nao ha Activity herdada, e
        // sem abrir uma aqui todo o trabalho dele ficaria invisivel no tracing. Este
        // span e a raiz do trace de cada ciclo — os spans de dominio dos handlers
        // (pagamento.webhook, ia.*) penduram nele.
        using var atividade = VetlyTelemetry.Iniciar("worker.ciclo");

        var inicio = Stopwatch.GetTimestamp();

        try
        {
            using var escopo = _escopos.CreateScope();

            await ExecutarRotinasAsync(escopo.ServiceProvider, cancellationToken);
            await ExecutarJobsAsync(escopo.ServiceProvider, cancellationToken);
        }
        finally
        {
            // Ciclo que passa a durar mais que o intervalo de 30s e o sinal antecedente
            // de fila crescendo — aparece aqui muito antes de virar lembrete atrasado.
            VetlyTelemetry.DuracaoDoCicloDoWorker.Record(
                Stopwatch.GetElapsedTime(inicio).TotalMilliseconds);
        }
    }

    private async Task ExecutarRotinasAsync(IServiceProvider servicos, CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;

        foreach (var rotina in servicos.GetServices<IRotinaPeriodica>())
        {
            if (_ultimaExecucao.TryGetValue(rotina.Nome, out var ultima) && agora - ultima < rotina.Intervalo)
                continue;

            try
            {
                await rotina.ExecutarAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Uma rotina com problema nao pode impedir as outras de rodar
                _logger.LogError(ex, "Falha na rotina periodica {Rotina}.", rotina.Nome);
            }

            _ultimaExecucao[rotina.Nome] = agora;
        }
    }

    private async Task ExecutarJobsAsync(IServiceProvider servicos, CancellationToken cancellationToken)
    {
        var fila = servicos.GetRequiredService<IFilaDeJobs>();
        var handlers = servicos.GetServices<IJobHandler>().ToDictionary(h => h.Tipo);

        var elegiveis = await fila.ObterElegiveisAsync(DateTime.UtcNow, JobsPorCiclo);

        foreach (var job in elegiveis)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!handlers.TryGetValue(job.Tipo, out var handler))
            {
                job.RegistrarFalha($"Nao ha handler registrado para o tipo {job.Tipo}.", DateTime.UtcNow);

                // Falha de configuracao, nao de execucao: o job nunca vai drenar por si.
                // Precisa aparecer na mesma serie, ou some da contagem.
                VetlyTelemetry.JobsExecutados.Add(1,
                    new KeyValuePair<string, object?>("tipo", job.Tipo.ToString()),
                    new KeyValuePair<string, object?>("resultado", "sem-handler"));

                continue;
            }

            try
            {
                await handler.ExecutarAsync(job, cancellationToken);
                job.Concluir();

                VetlyTelemetry.JobsExecutados.Add(1,
                    new KeyValuePair<string, object?>("tipo", job.Tipo.ToString()),
                    new KeyValuePair<string, object?>("resultado", "sucesso"));
            }
            catch (Exception ex)
            {
                VetlyTelemetry.JobsExecutados.Add(1,
                    new KeyValuePair<string, object?>("tipo", job.Tipo.ToString()),
                    new KeyValuePair<string, object?>("resultado", "falha"));

                // Falha e retentada com espera crescente; esgotadas as tentativas, o job
                // fica registrado como falho para inspecao, em vez de sumir.
                job.RegistrarFalha(ex.Message, DateTime.UtcNow);

                _logger.LogWarning(ex,
                    "Job {Tipo} falhou (tentativa {Tentativa}/{Maximo}).",
                    job.Tipo, job.Tentativas, Vetly.Domain.Entities.Job.MaximoDeTentativas);
            }
        }

        if (elegiveis.Count > 0)
            await fila.SalvarAsync();
    }
}
