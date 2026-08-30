using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Comum;

/// <summary>
/// Endereço de um prestador — veterinário autônomo ou empresa. A latitude/longitude não é informada pelo cliente:
/// é derivada do endereço persistido pela geocodificação (RN-026).
/// </summary>
public class EnderecoDto
{
    [Required(ErrorMessage = "O CEP é obrigatório.")]
    [MaxLength(9)]
    public string Cep { get; set; } = string.Empty;

    [Required(ErrorMessage = "O logradouro é obrigatório.")]
    [MaxLength(200)]
    public string Logradouro { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Numero { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Complemento { get; set; }

    [MaxLength(150)]
    public string Bairro { get; set; } = string.Empty;

    [Required(ErrorMessage = "A cidade é obrigatória.")]
    [MaxLength(150)]
    public string Cidade { get; set; } = string.Empty;

    [Required(ErrorMessage = "A UF é obrigatória.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "A UF deve ter 2 caracteres.")]
    public string Uf { get; set; } = string.Empty;

    /// <summary>Latitude derivada do endereço. Somente leitura — ignorada na entrada (RN-026).</summary>
    public decimal? Latitude { get; set; }

    /// <summary>Longitude derivada do endereço. Somente leitura — ignorada na entrada (RN-026).</summary>
    public decimal? Longitude { get; set; }

    /// <summary>Sinaliza coordenada de baixa precisão, pendente de revisão. Somente leitura.</summary>
    public bool CoordenadaRevisar { get; set; }
}
