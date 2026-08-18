namespace Vetly.Application.DTOs.IA;

/// <summary>Resposta da sugestão de protocolo (RN-096.2) — medicamentos + alertas de interação + id do log pendente.</summary>
public class SugestaoProtocoloResponseDto
{
    public List<string> Medicamentos { get; set; } = [];
    public List<string> AlertasInteracao { get; set; } = [];
    public string DuracaoEstimada { get; set; } = string.Empty;
    public string Observacoes { get; set; } = string.Empty;
    public Guid LogId { get; set; }
}
