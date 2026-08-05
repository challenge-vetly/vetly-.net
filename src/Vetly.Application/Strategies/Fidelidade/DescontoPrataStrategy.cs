using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Fidelidade;

/// <summary>Tier Prata: 5% de desconto em serviços, incidência 3% Vetly + 2% veterinário (RN-072).</summary>
public class DescontoPrataStrategy : IDescontoFidelidadeStrategy
{
    public bool Aplicavel(TierFidelidade tier) => tier == TierFidelidade.Prata;
    public decimal PercentualDesconto => 5m;
    public decimal PercentualIncidenciaVetly => 3m;
    public decimal PercentualIncidenciaVeterinario => 2m;
}
