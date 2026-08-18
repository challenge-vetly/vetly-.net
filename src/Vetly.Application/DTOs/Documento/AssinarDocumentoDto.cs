using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Documento;

/// <summary>DTO de entrada para assinar um documento por nome digitado (RN-031 — MVP).</summary>
public class AssinarDocumentoDto
{
    [Required(ErrorMessage = "O nome digitado para assinatura é obrigatório.")]
    [MaxLength(200)]
    public string NomeDigitado { get; set; } = string.Empty;
}
