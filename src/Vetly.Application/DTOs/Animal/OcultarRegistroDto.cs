using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Animal;

/// <summary>DTO de entrada para ocultar um prontuário da visão do veterinário (RN-088).</summary>
public class OcultarRegistroDto
{
    [Required(ErrorMessage = "O id do prontuário é obrigatório.")]
    public Guid ProntuarioId { get; set; }
}
