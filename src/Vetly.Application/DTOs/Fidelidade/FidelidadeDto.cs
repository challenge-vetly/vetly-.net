using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Fidelidade;

/// <summary>DTO de resposta com o resumo de fidelidade de um responsável (RN-071).</summary>
public class FidelidadeDto
{
    public Guid ResponsavelId { get; set; }
    public TierFidelidade TierFidelidade { get; set; }
    public int SaldoPontos { get; set; }

    /// <summary>Pontos que faltam para o próximo tier. Nulo quando já no Ouro (tier máximo).</summary>
    public int? PontosParaProximoTier { get; set; }
}
