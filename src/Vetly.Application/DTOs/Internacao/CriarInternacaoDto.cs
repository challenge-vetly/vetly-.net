using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Internacao;

/// <summary>DTO de entrada para abertura de uma nova internação.</summary>
public class CriarInternacaoDto
{
    [Required(ErrorMessage = "O animal é obrigatório.")]
    public Guid AnimalId { get; set; }

    [Required(ErrorMessage = "O veterinário é obrigatório.")]
    public Guid VeterinarioId { get; set; }

    [Required]
    [Range(0, double.MaxValue, ErrorMessage = "O valor da caução deve ser positivo.")]
    public decimal ValorCaucao { get; set; }
}
