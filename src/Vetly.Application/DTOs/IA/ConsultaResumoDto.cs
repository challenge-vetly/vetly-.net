namespace Vetly.Application.DTOs.IA;

/// <summary>Resumo de consulta enviado ao Ollama para geração de orientações pós-atendimento.</summary>
public class ConsultaResumoDto
{
    public string Especie { get; set; } = string.Empty;
    public string Diagnostico { get; set; } = string.Empty;
    public List<string> Medicamentos { get; set; } = [];
    public string Conduta { get; set; } = string.Empty;
    public string? RestricoesDieta { get; set; }
}
