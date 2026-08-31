using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Captura;

/// <summary>
/// Rascunho de prontuário produzido pela IA (RN-080, §7.3).
///
/// A resposta diz, sempre, que é rascunho e de onde ele veio: o veterinário decide
/// sobre ele, e a decisão é dele (RN-082).
/// </summary>
public class RascunhoIaDto
{
    public Guid Id { get; set; }
    public Guid ConsultaId { get; set; }

    /// <summary>Estado do ciclo de documentação (§7.3).</summary>
    public EstadoSessaoCaptura EstadoDaSessao { get; set; }

    public string Anamnese { get; set; } = string.Empty;
    public string ExameFisico { get; set; } = string.Empty;
    public List<string> HipotesesDiagnosticas { get; set; } = [];
    public string Conduta { get; set; } = string.Empty;
    public string Orientacoes { get; set; } = string.Empty;

    /// <summary>Transcrição que originou o rascunho — permite conferir cada frase.</summary>
    public string TextoOrigem { get; set; } = string.Empty;

    /// <summary>Modelo e versão que produziram o rascunho.</summary>
    public string? Modelo { get; set; }

    /// <summary>Saiu de uma transcrição incompleta: falta áudio, e isso precisa aparecer.</summary>
    public bool Parcial { get; set; }

    /// <summary>Avisos que acompanham o rascunho. Ex.: <c>TranscricaoParcial</c>, <c>PesoAusente</c>.</summary>
    public List<string> Avisos { get; set; } = [];

    public DateTime GeradoEm { get; set; }
    public int DuracaoMs { get; set; }
}
