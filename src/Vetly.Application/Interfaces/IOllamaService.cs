using Vetly.Application.DTOs.IA;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato do serviço de integração com o Ollama (LLM local).
/// Todos os métodos retornam apenas sugestões — o veterinário deve validar (RN-024).
/// </summary>
public interface IOllamaService
{
    /// <summary>Sugere hipóteses diagnósticas com base no contexto clínico do animal.</summary>
    Task<List<HipoteseDiagnosticaDto>> SugerirDiagnosticoAsync(ContextoClinicoDto contexto);

    /// <summary>Sugere protocolo de tratamento dado um diagnóstico, espécie e peso.</summary>
    Task<ProtocoloTratamentoDto> SugerirProtocoloAsync(string diagnostico, string especie, decimal pesoKg);

    /// <summary>Gera orientações pós-atendimento personalizadas para o responsavel.</summary>
    Task<string> GerarOrientacoesPostAtendimentoAsync(ConsultaResumoDto consulta);

    /// <summary>Realiza triagem de sintomas e retorna nível de urgência e recomendações.</summary>
    Task<TriagemResultadoDto> RealizarTriagemAsync(SintomasDto sintomas);
}
