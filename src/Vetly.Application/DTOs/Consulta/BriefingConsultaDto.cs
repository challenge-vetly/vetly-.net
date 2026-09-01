using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Exame;

namespace Vetly.Application.DTOs.Consulta;

/// <summary>DTO de briefing pre-consulta com contexto clinico agregado do animal.</summary>
public class BriefingConsultaDto
{
    public Guid ConsultaId { get; set; }
    public AnimalDto Animal { get; set; } = null!;
    public List<ConsultaDto> HistoricoResumido { get; set; } = [];
    public List<string> AlertasAtivos { get; set; } = [];
    public List<ExameDto> ExamesRecentes { get; set; } = [];
    public DateTime? UltimaConsulta { get; set; }

    /// <summary>Peso do animal. Nulo impede sugestão de dose pela IA (RN-081).</summary>
    public decimal? PesoKg { get; set; }

    /// <summary>Alergias conhecidas — alerta de segurança, nunca ocultável (RN-068).</summary>
    public List<string> Alergias { get; set; } = [];

    public List<string> CondicoesPreexistentes { get; set; } = [];

    /// <summary>
    /// Pré-sintomas informados pelo Responsável no agendamento (RN-005/RN-036). Nulo
    /// em consulta de emergência, que não teve agendamento.
    /// </summary>
    public PreSintomasDto? PreSintomas { get; set; }

    /// <summary>Fotos e vídeos anexados aos pré-sintomas.</summary>
    public List<Guid> PreSintomasMidias { get; set; } = [];

    /// <summary>
    /// Falso quando o veterinário não tem colmeia vigente e está vendo apenas o que
    /// ele mesmo produziu (RN-066). Dizer isso evita que ele leia "sem histórico"
    /// como "animal sem passado clínico".
    /// </summary>
    public bool HistoricoCompleto { get; set; }
}
