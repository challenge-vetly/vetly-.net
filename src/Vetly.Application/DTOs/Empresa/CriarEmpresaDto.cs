using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Empresa;

/// <summary>DTO de entrada para cadastro de uma nova empresa.</summary>
public class CriarEmpresaDto
{
    [Required(ErrorMessage = "O nome da empresa é obrigatório.")]
    [MaxLength(300)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "O tipo da empresa é obrigatório.")]
    [MaxLength(100)]
    public string Tipo { get; set; } = string.Empty;

    [Required(ErrorMessage = "O id do administrador é obrigatório.")]
    public Guid AdministradorId { get; set; }
}
