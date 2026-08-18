using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Fidelidade;

/// <summary>
/// Contrato do Strategy Pattern para o desconto de fidelidade em serviços, por tier
/// (RN-071/072). A incidência é compartilhada entre Vetly e veterinário — cada strategy
/// já sabe sua própria divisão.
/// </summary>
public interface IDescontoFidelidadeStrategy
{
    /// <summary>Indica se esta strategy é aplicável ao tier informado.</summary>
    bool Aplicavel(TierFidelidade tier);

    /// <summary>Percentual total de desconto em serviços para este tier.</summary>
    decimal PercentualDesconto { get; }

    /// <summary>Parcela do desconto (em pontos percentuais do valor do serviço) absorvida pela Vetly.</summary>
    decimal PercentualIncidenciaVetly { get; }

    /// <summary>Parcela do desconto (em pontos percentuais do valor do serviço) absorvida pelo veterinário.</summary>
    decimal PercentualIncidenciaVeterinario { get; }
}
