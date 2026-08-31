using Microsoft.Extensions.Logging;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.Jobs;

/// <summary>
/// Oferece um horário liberado ao primeiro da lista de espera (RN-037).
///
/// Enfileirado sempre que um horário volta a ficar livre — cancelamento, remarcação
/// ou lock vencido. É o que faz o Responsável na fila ser avisado sem ninguém do
/// outro lado apertar nada.
/// </summary>
public class PromoverListaEsperaHandler : IJobHandler
{
    private readonly IListaEsperaService _listaEspera;
    private readonly ILogger<PromoverListaEsperaHandler> _logger;

    public PromoverListaEsperaHandler(
        IListaEsperaService listaEspera, ILogger<PromoverListaEsperaHandler> logger)
    {
        _listaEspera = listaEspera;
        _logger = logger;
    }

    /// <inheritdoc/>
    public TipoJob Tipo => TipoJob.PromoverListaEspera;

    /// <inheritdoc/>
    public async Task ExecutarAsync(Job job, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(job.Payload, out var slotId))
            throw new InvalidOperationException("Payload do job nao contem um id de horario valido.");

        var promovido = await _listaEspera.PromoverProximoAsync(slotId);

        if (promovido is null)
        {
            _logger.LogInformation("Horario {SlotId} liberado sem ninguem na fila de espera.", slotId);
            return;
        }

        _logger.LogInformation(
            "Vaga oferecida ao proximo da fila | item={ItemId} horario={SlotId} prioridadeAte={Prioridade}",
            promovido.Id, slotId, promovido.PrioridadeAte);
    }
}

/// <summary>
/// Entrega o webhook do provedor simulado (§5.1).
///
/// No provedor real é ele quem chama a Vetly quando o pagamento muda de estado. Como
/// não há provedor, este job faz o mesmo caminho: chama o processamento de webhook
/// com o payload que o provedor mandaria.
///
/// Por isso o atraso de 2 segundos — o pagamento não pode confirmar dentro da mesma
/// requisição que criou a cobrança, senão o fluxo perderia justamente a forma
/// assíncrona que precisa ter quando um gateway real entrar.
/// </summary>
public class ConfirmarPagamentoSimuladoHandler : IJobHandler
{
    private readonly IPagamentoService _pagamentos;
    private readonly ITokenDeServico _token;
    private readonly ILogger<ConfirmarPagamentoSimuladoHandler> _logger;

    public ConfirmarPagamentoSimuladoHandler(
        IPagamentoService pagamentos, ITokenDeServico token,
        ILogger<ConfirmarPagamentoSimuladoHandler> logger)
    {
        _pagamentos = pagamentos;
        _token = token;
        _logger = logger;
    }

    /// <inheritdoc/>
    public TipoJob Tipo => TipoJob.ConfirmarPagamentoSimulado;

    /// <inheritdoc/>
    public async Task ExecutarAsync(Job job, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(job.Payload))
            throw new InvalidOperationException("Payload do job de pagamento esta vazio.");

        var resultado = await _pagamentos.ProcessarWebhookAsync(job.Payload, _token.Valor);

        _logger.LogInformation(
            "Webhook simulado processado | pagamento={PagamentoId} status={Status} ignorado={Ignorado}",
            resultado.PagamentoId, resultado.StatusPagamento, resultado.Ignorado);
    }
}
