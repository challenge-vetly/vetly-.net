using System.ComponentModel.DataAnnotations;

namespace Vetly.Application.DTOs.Exame;

/// <summary>DTO de entrada para registro do resultado de um exame.</summary>
public class RegistrarResultadoExameDto
{
    [Required(ErrorMessage = "O resultado e obrigatorio.")]
    [MinLength(1)]
    public string Resultado { get; set; } = string.Empty;

    /// <summary>
    /// Mídias do laudo (PDF, imagem), já enviadas ao storage (§2.6). Resultado de
    /// exame raramente é só texto, e obrigar o veterinário a transcrever o que já
    /// existe em arquivo é onde o dado se perde.
    /// </summary>
    public List<Guid> MidiaIds { get; set; } = [];
}
