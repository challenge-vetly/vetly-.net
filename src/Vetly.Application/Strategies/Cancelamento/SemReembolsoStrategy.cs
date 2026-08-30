using Vetly.Application.DTOs.Cancelamento;
using Vetly.Domain.Entities;

namespace Vetly.Application.Strategies.Cancelamento;

/// <summary>
/// RN-041: Cancelamento com menos de 2h de antecedência ou no ato → sem reembolso.
/// É a política de fallback (prioridade 3) — aplicada quando as outras não se aplicam.
/// </summary>
public class SemReembolsoStrategy : ICancelamentoStrategy
{
    /// <inheritdoc/>
    public int Prioridade => 3;

    /// <inheritdoc/>
    public bool Aplicavel(DateTime horarioConsulta, DateTime horarioCancelamento)
    {
        // Aplica sempre que as outras strategies não se aplicarem (menos de 2h ou após o horário)
        var antecedencia = horarioConsulta - horarioCancelamento;
        return antecedencia.TotalHours <= 2;
    }

    /// <inheritdoc/>
    public ResultadoCancelamentoDto Executar(Pagamento pagamento, decimal percentualRetencao)
    {
        return new ResultadoCancelamentoDto
        {
            ValorReembolso = 0,
            PercentualRetencao = 100,
            EstrategiaAplicada = "Sem Reembolso",
            Descricao = "Cancelamento com menos de 2h de antecedência ou no ato. Sem direito a reembolso."
        };
    }
}
