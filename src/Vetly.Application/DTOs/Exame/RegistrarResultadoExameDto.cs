using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Exame;

/// <summary>DTO de entrada para registro do resultado de um exame.</summary>
public class RegistrarResultadoExameDto
{
    [Required(ErrorMessage = "O resultado e obrigatorio.")]
    [MinLength(1)]
    public string Resultado { get; set; } = string.Empty;
}
