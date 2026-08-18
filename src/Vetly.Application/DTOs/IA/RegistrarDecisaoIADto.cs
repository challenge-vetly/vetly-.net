using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.IA;

/// <summary>DTO de entrada para a decisão do veterinário sobre uma sugestão de IA (RN-099).</summary>
public class RegistrarDecisaoIADto
{
    [Required(ErrorMessage = "O tipo é obrigatório.")]
    public TipoSugestaoIA Tipo { get; set; }

    [Required(ErrorMessage = "A decisão é obrigatória.")]
    public DecisaoVeterinario Decisao { get; set; }

    /// <summary>Obrigatório quando Decisao = Corrigir — o texto do vet vira o estado final autoritativo.</summary>
    public string? ConteudoCorrigido { get; set; }
}
