using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Split;

/// <summary>
/// Base das strategies de split por plano (RN-070). Concentra a aritmética; cada
/// plano concreto informa apenas o seu take rate.
/// </summary>
public abstract class SplitPorPlanoStrategy : ISplitFinanceiroStrategy
{
    /// <summary>Plano coberto por esta strategy.</summary>
    protected abstract PlanoAssinatura Plano { get; }

    /// <summary>Percentual retido pela Vetly neste plano, de 0 a 100 (RN-070).</summary>
    protected abstract decimal TakeRate { get; }

    /// <inheritdoc/>
    public bool Aplicavel(PlanoAssinatura plano) => plano == Plano;

    /// <inheritdoc/>
    public ResultadoDoSplit Calcular(decimal valorBruto)
    {
        if (valorBruto < 0)
            throw new ArgumentOutOfRangeException(nameof(valorBruto), "O valor não pode ser negativo.");

        // Arredonda a comissão e deriva o repasse por subtração: assim os dois sempre
        // somam exatamente o valor bruto, sem centavo sobrando nem faltando.
        var comissao = Math.Round(valorBruto * TakeRate / 100m, 2, MidpointRounding.AwayFromZero);

        return new ResultadoDoSplit(Plano, TakeRate, comissao, valorBruto - comissao);
    }
}

/// <summary>
/// Plano Básico: assinatura R$ 0 e take rate de 15% (RN-070/RN-072).
/// O freemium remove a barreira de entrada e povoa o matching — sem oferta, o
/// marketplace não existe.
/// </summary>
public class SplitBasicoStrategy : SplitPorPlanoStrategy
{
    /// <inheritdoc/>
    protected override PlanoAssinatura Plano => PlanoAssinatura.Basico;

    /// <inheritdoc/>
    protected override decimal TakeRate => 15m;
}

/// <summary>
/// Plano Profissional: assinatura mensal e take rate de 12% (RN-070/RN-072).
/// </summary>
public class SplitProfissionalStrategy : SplitPorPlanoStrategy
{
    /// <inheritdoc/>
    protected override PlanoAssinatura Plano => PlanoAssinatura.Profissional;

    /// <inheritdoc/>
    protected override decimal TakeRate => 12m;
}

/// <summary>
/// Plano Enterprise: assinatura por faixa de vets e take rate de 10% (RN-070/RN-072).
/// </summary>
public class SplitEnterpriseStrategy : SplitPorPlanoStrategy
{
    /// <inheritdoc/>
    protected override PlanoAssinatura Plano => PlanoAssinatura.Enterprise;

    /// <inheritdoc/>
    protected override decimal TakeRate => 10m;
}
