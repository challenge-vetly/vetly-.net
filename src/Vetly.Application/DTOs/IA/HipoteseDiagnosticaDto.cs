namespace Vetly.Application.DTOs.IA;

/// <summary>
/// Hipótese diagnóstica retornada pelo Ollama.
/// RN-024: são apenas sugestões — o veterinário deve validar manualmente antes de gerar documentos.
/// </summary>
public class HipoteseDiagnosticaDto
{
    /// <summary>Nome da hipótese diagnóstica sugerida.</summary>
    public string Hipotese { get; set; } = string.Empty;

    /// <summary>Nível de confiança da IA (baixo/médio/alto).</summary>
    public string NivelConfianca { get; set; } = string.Empty;

    /// <summary>Justificativa clínica para a hipótese.</summary>
    public string Justificativa { get; set; } = string.Empty;
}
