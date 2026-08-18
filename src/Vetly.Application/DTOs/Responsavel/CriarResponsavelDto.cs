using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Responsavel;

/// <summary>DTO de entrada para cadastro de um novo responsavel.</summary>
public class CriarResponsavelDto
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "O telefone é obrigatório.")]
    [MaxLength(20)]
    public string Telefone { get; set; } = string.Empty;
}
