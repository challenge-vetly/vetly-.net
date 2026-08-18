using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Avaliacao;

/// <summary>DTO de entrada para a resposta pública do veterinário a uma avaliação (RN-079).</summary>
public class ResponderAvaliacaoDto
{
    [Required(ErrorMessage = "A resposta é obrigatória.")]
    [MaxLength(2000)]
    public string Resposta { get; set; } = string.Empty;
}
