using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Responsavel;

/// <summary>DTO de resposta com um registro (histórico ou ativo) de consentimento LGPD.</summary>
public class ConsentimentoLgpdDto
{
    public Guid Id { get; set; }
    public Guid ResponsavelId { get; set; }
    public FinalidadeConsentimento Finalidade { get; set; }
    public bool Ativo { get; set; }
    public DateTime DataConcessao { get; set; }
    public DateTime? DataRevogacao { get; set; }
}
