using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Animal;

/// <summary>Uma entrada da carteira de vacinação do animal.</summary>
public class RegistroVacinacaoDto
{
    /// <summary>Tipo/nome da vacina aplicada (ex: "V10", "Antirrábica").</summary>
    [Required(ErrorMessage = "O tipo da vacina é obrigatório.")]
    [MaxLength(100)]
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Data em que a vacina foi aplicada (UTC).</summary>
    [Required(ErrorMessage = "A data de aplicação é obrigatória.")]
    public DateTime AplicadaEm { get; set; }
}
