using System.ComponentModel.DataAnnotations;
using Vetly.Application.DTOs.Comum;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Veterinario;

/// <summary>DTO de entrada para cadastro de um novo veterinário.</summary>
public class CriarVeterinarioDto
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    /// <summary>CRMV no formato XXXXXX-UF (ex: 12345-SP).</summary>
    [Required(ErrorMessage = "O CRMV é obrigatório.")]
    [RegularExpression(@"^\d{4,6}-[A-Z]{2}$", ErrorMessage = "CRMV inválido. Use o formato XXXXXX-UF.")]
    public string Crmv { get; set; } = string.Empty;

    [Required]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "A UF deve ter 2 caracteres.")]
    public string UfAtuacao { get; set; } = string.Empty;

    [Required]
    public PersonaVeterinario Persona { get; set; }

    [Required]
    public PlanoAssinatura Plano { get; set; }

    /// <summary>
    /// Endereço do veterinário (RN-026). Opcional nesta fase: a obrigatoriedade entra
    /// junto com o IGeocodificacaoAdapter, quando a coordenada passa a ser derivável.
    /// </summary>
    public EnderecoDto? Endereco { get; set; }

    public List<string> Especialidades { get; set; } = [];
    public List<string> EspeciesAtendidas { get; set; } = [];
    public string? TitulacaoAcademica { get; set; }
}
