using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Avaliacao;

/// <summary>DTO de entrada para moderar o comentário de uma avaliação (RN-080).</summary>
public class ModerarAvaliacaoDto
{
    [Required]
    public StatusModeracao StatusModeracao { get; set; }
}
