namespace Vetly.Application.DTOs.IA;

/// <summary>Resposta da sugestão de diagnóstico (RN-096.1) — hipóteses + id do log de auditoria pendente.</summary>
public class SugestaoDiagnosticoResponseDto
{
    public List<HipoteseDiagnosticaDto> Hipoteses { get; set; } = [];
    public Guid LogId { get; set; }
}
