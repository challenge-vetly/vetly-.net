using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Exame;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>DTO de briefing pre-consulta com contexto clinico agregado do animal.</summary>
public class BriefingConsultaDto
{
    public Guid ConsultaId { get; set; }
    public AnimalDto Animal { get; set; } = null!;
    public string? PreSintomas { get; set; }
    public List<ConsultaDto> HistoricoResumido { get; set; } = [];
    public List<string> AlertasAtivos { get; set; } = [];
    public List<ExameDto> ExamesRecentes { get; set; } = [];
    public DateTime? UltimaConsulta { get; set; }
}
