namespace Vetly.Application.DTOs.IA;

/// <summary>Protocolo de tratamento sugerido pelo Ollama dado um diagnóstico.</summary>
public class ProtocoloTratamentoDto
{
    public string Diagnostico { get; set; } = string.Empty;
    public List<string> Medicamentos { get; set; } = [];
    public List<string> Procedimentos { get; set; } = [];
    public string DuracaoEstimada { get; set; } = string.Empty;
    public string Observacoes { get; set; } = string.Empty;
}
