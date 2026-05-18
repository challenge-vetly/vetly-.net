using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Internacao;

/// <summary>DTO de entrada para registro de procedimentos diarios de uma internacao.</summary>
public class RegistrarProcedimentosDto
{
    [Required]
    [MinLength(1, ErrorMessage = "Informe ao menos um procedimento.")]
    public List<ProcedimentoDiarioDto> Procedimentos { get; set; } = [];
}

/// <summary>Item individual de procedimento realizado em um dia da internacao.</summary>
public class ProcedimentoDiarioDto
{
    [Required]
    public DateTime Data { get; set; }

    [Required]
    [MaxLength(300)]
    public string Procedimento { get; set; } = string.Empty;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "O valor do procedimento deve ser maior que zero.")]
    public decimal Valor { get; set; }
}
