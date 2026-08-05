using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

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

    [Required(ErrorMessage = "O sexo é obrigatório.")]
    public SexoAnimal Sexo { get; set; }

    [Required(ErrorMessage = "A data de nascimento é obrigatória.")]
    public DateTime DataNascimento { get; set; }

    [Required(ErrorMessage = "O id do responsavel é obrigatório.")]
    public Guid ResponsavelId { get; set; }

    public bool Castrado { get; set; }

    public decimal? PesoKg { get; set; }

    [MaxLength(500)]
    public string? FotoUrl { get; set; }

    public List<string> CondicoesPreExistentes { get; set; } = [];
    public List<string> Alergias { get; set; } = [];
    public List<string> CarteiraVacinacao { get; set; } = [];
    public List<string> MedicacoesEmUso { get; set; } = [];
}
