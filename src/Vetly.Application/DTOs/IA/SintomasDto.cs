using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.IA;

/// <summary>DTO de entrada para triagem de sintomas via Ollama.</summary>
public class SintomasDto
{
    [Required]
    public string Especie { get; set; } = string.Empty;

    [Required]
    public List<string> Sintomas { get; set; } = [];

    public int? IdadeAnos { get; set; }
    public decimal? PesoKg { get; set; }
}
