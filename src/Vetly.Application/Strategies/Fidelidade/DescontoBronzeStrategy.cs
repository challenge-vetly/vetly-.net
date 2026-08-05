using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Fidelidade;

/// <summary>Tier Bronze: sem desconto de fidelidade (RN-071).</summary>
public class DescontoBronzeStrategy : IDescontoFidelidadeStrategy
{
    public bool Aplicavel(TierFidelidade tier) => tier == TierFidelidade.Bronze;
    public decimal PercentualDesconto => 0m;
    public decimal PercentualIncidenciaVetly => 0m;
    public decimal PercentualIncidenciaVeterinario => 0m;
}
