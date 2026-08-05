using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Comissao;

/// <summary>
/// Contrato do Strategy Pattern para o percentual de comissão retido pela plataforma
/// por transação, conforme o plano de assinatura do veterinário (RN-089).
/// Complementar a <see cref="Vetly.Application.Strategies.Split.ISplitFinanceiroStrategy"/>:
/// esta decide QUANTO a plataforma retém; a de split decide QUEM recebe o repasse
/// (vet autônomo × empresa).
/// </summary>
public interface IComissaoStrategy
{
    /// <summary>Indica se esta strategy é aplicável ao plano informado.</summary>
    bool Aplicavel(PlanoAssinatura plano);

    /// <summary>Percentual de comissão retido pela plataforma para este plano.</summary>
    decimal PercentualComissao { get; }
}
