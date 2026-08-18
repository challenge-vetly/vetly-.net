using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Responsavel;

/// <summary>DTO de entrada para conceder um novo consentimento LGPD.</summary>
public class ConcederConsentimentoDto
{
    [Required(ErrorMessage = "A finalidade é obrigatória.")]
    public FinalidadeConsentimento Finalidade { get; set; }
}
