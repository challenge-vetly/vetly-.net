using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.IA;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Controller de IA assistente via Ollama.
/// Todos os endpoints retornam apenas sugestoes — o veterinario deve validar (RN-082).
/// </summary>
[ApiController]
[Route("api/ia")]
[Authorize]
public class OllamaController : ControllerBase
{
    private readonly IOllamaService _service;

    public OllamaController(IOllamaService service) => _service = service;

    /// <summary>
    /// Sugere hipoteses diagnosticas com base no contexto clinico (RN-082).
    /// O resultado e uma sugestao de IA — deve ser validado pelo veterinario.
    /// </summary>
    [HttpPost("diagnostico")]
    [ProducesResponseType(typeof(IEnumerable<HipoteseDiagnosticaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SugerirDiagnostico([FromBody] ContextoClinicoDto contexto) =>
        Ok(await _service.SugerirDiagnosticoAsync(contexto));

    /// <summary>Sugere um protocolo de tratamento dado um diagnostico, especie e peso.</summary>
    [HttpPost("protocolo")]
    [ProducesResponseType(typeof(ProtocoloTratamentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SugerirProtocolo([FromBody] ProtocoloRequest request) =>
        Ok(await _service.SugerirProtocoloAsync(request.Diagnostico, request.Especie, request.PesoKg));

    /// <summary>Realiza triagem de sintomas e retorna nivel de urgencia e recomendacoes.</summary>
    [HttpPost("triagem")]
    [ProducesResponseType(typeof(TriagemResultadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RealizarTriagem([FromBody] SintomasDto sintomas) =>
        Ok(await _service.RealizarTriagemAsync(sintomas));

    /// <summary>Gera orientacoes pos-atendimento personalizadas para o tutor.</summary>
    [HttpPost("orientacoes")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GerarOrientacoes([FromBody] ConsultaResumoDto consulta) =>
        Ok(await _service.GerarOrientacoesPostAtendimentoAsync(consulta));
}

/// <summary>Payload para solicitacao de protocolo de tratamento.</summary>
public sealed class ProtocoloRequest
{
    public string Diagnostico { get; set; } = string.Empty;
    public string Especie { get; set; } = string.Empty;
    public decimal PesoKg { get; set; }
}
