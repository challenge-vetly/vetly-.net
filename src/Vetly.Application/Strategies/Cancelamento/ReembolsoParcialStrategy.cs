using Vetly.Application.DTOs.Cancelamento;
using Vetly.Domain.Entities;

namespace Vetly.Application.Strategies.Cancelamento;

/// <summary>
/// RN-041/RN-042: Cancelamento entre 24h e 2h de antecedência → reembolso parcial,
/// com percentual de retenção configurado pela clínica (RN-042).
/// A taxa de retenção é configurável via appsettings (percentualRetencao).
/// </summary>
public class ReembolsoParcialStrategy : ICancelamentoStrategy
{
    /// <inheritdoc/>
    public int Prioridade => 2;

    /// <inheritdoc/>
    public bool Aplicavel(DateTime horarioConsulta, DateTime horarioCancelamento)
    {
        var antecedencia = horarioConsulta - horarioCancelamento;
        // Entre 2h e 24h de antecedência
        return antecedencia.TotalHours is > 2 and <= 24;
    }

    /// <inheritdoc/>
    public ResultadoCancelamentoDto Executar(Pagamento pagamento, decimal percentualRetencao)
    {
        var valorRetido = pagamento.Valor * (percentualRetencao / 100);
        var valorReembolso = pagamento.Valor - valorRetido;

        return new ResultadoCancelamentoDto
        {
            ValorReembolso = Math.Round(valorReembolso, 2),
            PercentualRetencao = percentualRetencao,
            EstrategiaAplicada = "Reembolso Parcial",
            Descricao = $"Cancelamento com entre 2h e 24h de antecedência. Taxa de retenção de {percentualRetencao}% aplicada."
        };
    }
}
