using Microsoft.Extensions.Logging;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.Jobs;

/// <summary>
/// Credita os pontos de uma consulta realizada (RN-052).
///
/// Roda fora da requisição porque o crédito não faz parte de encerrar a consulta: o
/// veterinário não pode ficar esperando o programa de fidelidade para fechar o
/// atendimento, e uma falha aqui não pode desfazer o que já aconteceu.
/// </summary>
public class CreditarPontosHandler : IJobHandler
{
    private readonly IFidelidadeService _fidelidade;
    private readonly ILogger<CreditarPontosHandler> _logger;

    public CreditarPontosHandler(IFidelidadeService fidelidade, ILogger<CreditarPontosHandler> logger)
    {
        _fidelidade = fidelidade;
        _logger = logger;
    }

    /// <inheritdoc/>
    public TipoJob Tipo => TipoJob.CreditarPontosDaConsulta;

    /// <inheritdoc/>
    public async Task ExecutarAsync(Job job, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(job.Payload, out var consultaId))
            throw new InvalidOperationException("Payload do job nao contem um id de consulta valido.");

        var movimento = await _fidelidade.CreditarPorConsultaAsync(consultaId);

        if (movimento is null)
        {
            // Consulta não realizada, pagamento não confirmado ou crédito já lançado.
            // Nenhum dos três é erro: o job simplesmente não tinha o que fazer.
            _logger.LogInformation("Nada a creditar para a consulta {ConsultaId}.", consultaId);
            return;
        }

        _logger.LogInformation(
            "Creditados {Pontos} ponto(s) pela consulta {ConsultaId}.", movimento.Pontos, consultaId);
    }
}

/// <summary>
/// Baixa os créditos de fidelidade vencidos (RN-051).
///
/// Roda uma vez por dia porque a validade é anual: varrer de minuto em minuto seria
/// consultar a base inteira para achar nada.
///
/// A baixa entra como lançamento no extrato, e não como saldo caindo sozinho — o
/// Responsável precisa poder ver por que o número mudou.
/// </summary>
public class ExpirarPontosVencidos : IRotinaPeriodica
{
    private readonly IFidelidadeService _fidelidade;
    private readonly ILogger<ExpirarPontosVencidos> _logger;

    public ExpirarPontosVencidos(IFidelidadeService fidelidade, ILogger<ExpirarPontosVencidos> logger)
    {
        _fidelidade = fidelidade;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Nome => "ExpirarPontosVencidos";

    /// <inheritdoc/>
    public TimeSpan Intervalo => TimeSpan.FromHours(24);

    /// <inheritdoc/>
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken)
    {
        var expirados = await _fidelidade.ExpirarVencidosAsync();

        if (expirados > 0)
            _logger.LogInformation("Expirados {Pontos} ponto(s) de fidelidade.", expirados);

        return expirados;
    }
}
