using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Exame;

/// <summary>DTO de entrada para solicitação de um novo exame.</summary>
public class CriarExameDto
{
    [Required(ErrorMessage = "O animal é obrigatório.")]
    public Guid AnimalId { get; set; }

    [Required(ErrorMessage = "O veterinário é obrigatório.")]
    public Guid VeterinarioId { get; set; }

    [Required(ErrorMessage = "O tipo de solicitação é obrigatório.")]
    [MaxLength(200)]
    public string TipoSolicitacao { get; set; } = string.Empty;
}
