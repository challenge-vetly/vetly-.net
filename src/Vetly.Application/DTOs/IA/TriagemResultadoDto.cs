namespace Vetly.Application.DTOs.IA;

/// <summary>Resultado da triagem de sintomas realizada pelo Ollama.</summary>
public class TriagemResultadoDto
{
    /// <summary>Nível de urgência: Baixo, Médio, Alto, Emergência.</summary>
    public string NivelUrgencia { get; set; } = string.Empty;

    /// <summary>Recomendação de conduta para o tutor.</summary>
    public string Recomendacao { get; set; } = string.Empty;

    /// <summary>Possíveis causas dos sintomas identificadas pela triagem.</summary>
    public List<string> PossiveisCausas { get; set; } = [];
}
