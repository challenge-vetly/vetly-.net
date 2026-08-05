using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Fidelidade;

/// <summary>
/// DTO de resposta com o desconto de fidelidade calculado e exibido (RN-072) — sem
/// abatimento real, já que o pagamento é simulado no MVP.
/// </summary>
public class ResultadoDescontoFidelidadeDto
{
    public TierFidelidade TierFidelidade { get; set; }
    public decimal PercentualDesconto { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal IncidenciaVetly { get; set; }
    public decimal IncidenciaVeterinario { get; set; }

    /// <summary>True quando o desconto foi zerado por penalidade de no-show (RN-064), mesmo com tier elegível.</summary>
    public bool BloqueadoPorPenalidade { get; set; }
}
