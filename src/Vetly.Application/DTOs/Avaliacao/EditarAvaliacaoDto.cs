using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Avaliacao;

/// <summary>DTO de entrada para editar uma avaliação dentro da janela de 48h (RN-082).</summary>
public class EditarAvaliacaoDto
{
    [Range(1, 5, ErrorMessage = "A nota geral deve estar entre 1 e 5.")]
    public int NotaGeral { get; set; }

    [Range(1, 5)]
    public int? NotaAtendimento { get; set; }

    [Range(1, 5)]
    public int? NotaPontualidade { get; set; }

    [Range(1, 5)]
    public int? NotaEstrutura { get; set; }

    [Range(1, 5)]
    public int? NotaCustoBeneficio { get; set; }

    [MaxLength(2000)]
    public string? Comentario { get; set; }
}
