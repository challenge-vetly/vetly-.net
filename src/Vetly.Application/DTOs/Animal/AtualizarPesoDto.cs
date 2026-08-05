using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Animal;

/// <summary>DTO de entrada para atualizar o peso de um animal (RN-096.2).</summary>
public class AtualizarPesoDto
{
    [Required(ErrorMessage = "O peso é obrigatório.")]
    [Range(0.01, 1000, ErrorMessage = "O peso deve ser maior que zero.")]
    public decimal PesoKg { get; set; }
}
