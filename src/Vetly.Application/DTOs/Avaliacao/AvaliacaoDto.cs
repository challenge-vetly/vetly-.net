using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Avaliacao;

/// <summary>DTO de resposta com os dados de uma avaliação.</summary>
public class AvaliacaoDto
{
    public Guid Id { get; set; }
    public Guid ConsultaId { get; set; }
    public Guid ResponsavelId { get; set; }
    public Guid VeterinarioId { get; set; }
    public int NotaGeral { get; set; }
    public int? NotaAtendimento { get; set; }
    public int? NotaPontualidade { get; set; }
    public int? NotaEstrutura { get; set; }
    public int? NotaCustoBeneficio { get; set; }
    public string? Comentario { get; set; }
    public DateTime Data { get; set; }
    public StatusModeracao StatusModeracao { get; set; }
    public string? RespostaVeterinario { get; set; }
    public DateTime? DataResposta { get; set; }
    public bool Invalidada { get; set; }
}
