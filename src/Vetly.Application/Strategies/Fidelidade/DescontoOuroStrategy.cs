using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Fidelidade;

/// <summary>Tier Ouro: 10% de desconto em serviços, incidência 6% Vetly + 4% veterinário (RN-072).</summary>
public class DescontoOuroStrategy : IDescontoFidelidadeStrategy
{
    public bool Aplicavel(TierFidelidade tier) => tier == TierFidelidade.Ouro;
    public decimal PercentualDesconto => 10m;
    public decimal PercentualIncidenciaVetly => 6m;
    public decimal PercentualIncidenciaVeterinario => 4m;
}
