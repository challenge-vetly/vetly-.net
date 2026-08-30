using Vetly.Application.DTOs.Cancelamento;
using Vetly.Domain.Entities;

namespace Vetly.Application.Strategies.Cancelamento;

/// <summary>
/// RN-041: Cancelamento com mais de 24h de antecedência → reembolso integral ao tutor.
/// É a política mais favorável ao tutor, portanto tem prioridade 1 (verificada primeiro).
/// </summary>
public class ReembolsoIntegralStrategy : ICancelamentoStrategy
{
    /// <inheritdoc/>
    public int Prioridade => 1;

    /// <inheritdoc/>
    public bool Aplicavel(DateTime horarioConsulta, DateTime horarioCancelamento)
    {
        // Aplica quando a consulta é mais de 24h no futuro a partir do cancelamento
        var antecedencia = horarioConsulta - horarioCancelamento;
        return antecedencia.TotalHours > 24;
    }

    /// <inheritdoc/>
    public ResultadoCancelamentoDto Executar(Pagamento pagamento, decimal percentualRetencao)
    {
        // Reembolso de 100% — percentualRetencao é ignorado nesta política
        return new ResultadoCancelamentoDto
        {
            ValorReembolso = pagamento.Valor,
            PercentualRetencao = 0,
            EstrategiaAplicada = "Reembolso Integral",
            Descricao = "Cancelamento com mais de 24h de antecedência. Reembolso total do valor pago."
        };
    }
}
