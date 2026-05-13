using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Animal;

/// <summary>DTO de entrada para cadastro de um novo animal.</summary>
public class CriarAnimalDto
{
    [Required(ErrorMessage = "O nome do animal é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "A espécie é obrigatória.")]
    [MaxLength(100)]
    public string Especie { get; set; } = string.Empty;

    [Required(ErrorMessage = "A raça é obrigatória.")]
    [MaxLength(100)]
    public string Raca { get; set; } = string.Empty;

    [Required(ErrorMessage = "A data de nascimento é obrigatória.")]
    public DateTime DataNascimento { get; set; }

    [Required(ErrorMessage = "O id do tutor é obrigatório.")]
    public Guid TutorId { get; set; }
}
