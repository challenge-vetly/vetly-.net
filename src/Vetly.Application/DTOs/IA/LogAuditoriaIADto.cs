using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.IA;

/// <summary>DTO de resposta com um registro da trilha de auditoria de IA (RN-098).</summary>
public class LogAuditoriaIADto
{
    public Guid Id { get; set; }
    public Guid ConsultaId { get; set; }
    public Guid VeterinarioId { get; set; }
    public string Crmv { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string VersaoModelo { get; set; } = string.Empty;
    public TipoSugestaoIA TipoSugestao { get; set; }
    public string ConteudoSugerido { get; set; } = string.Empty;
    public DecisaoVeterinario? Decisao { get; set; }
    public string? ConteudoFinal { get; set; }
    public bool Pendente { get; set; }
}
