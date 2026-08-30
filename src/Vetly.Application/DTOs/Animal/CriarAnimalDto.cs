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

    [Required(ErrorMessage = "A data de nascimento é obrigatória.")]
    public DateTime DataNascimento { get; set; }

    [Required(ErrorMessage = "O id do tutor é obrigatório.")]
    public Guid TutorId { get; set; }

    /// <summary>
    /// Peso do animal em quilogramas. Obrigatório: sem peso a IA não pode sugerir
    /// posologia, e a sugestão de dose é onde mora o erro clínico (RN-081).
    /// </summary>
    [Required(ErrorMessage = "O peso é obrigatório — sem ele a IA não pode sugerir dose (RN-081).")]
    [Range(0.01, 999.99, ErrorMessage = "O peso deve estar entre 0,01 kg e 999,99 kg.")]
    public decimal PesoKg { get; set; }

    /// <summary>Sexo do animal.</summary>
    public SexoAnimal? Sexo { get; set; }

    /// <summary>Indica se o animal é castrado.</summary>
    public bool? Castrado { get; set; }

    /// <summary>Id da mídia com a foto do animal no storage de objetos.</summary>
    public Guid? FotoMidiaId { get; set; }

    /// <summary>Alergias conhecidas (ex: "Dipirona"). Viram alerta de segurança (RN-068).</summary>
    public List<string> Alergias { get; set; } = [];

    /// <summary>Condições pré-existentes (ex: "Displasia leve").</summary>
    public List<string> CondicoesPreexistentes { get; set; } = [];

    /// <summary>Carteira de vacinação — base do calendário de obrigações do pet (RN-046).</summary>
    public List<RegistroVacinacaoDto> CarteiraVacinacao { get; set; } = [];
}
