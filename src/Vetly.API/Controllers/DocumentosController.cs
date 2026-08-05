using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Documento;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.API.Controllers;

/// <summary>
/// Controller de documentos clinicos.
/// Geracao requer diagnostico validado (RN-024).
/// Assinatura digital (RN-031) e correcao de versao (RN-032/033/034).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentosController : ControllerBase
{
    private readonly IDocumentoService _service;

    public DocumentosController(IDocumentoService service) => _service = service;

    /// <summary>Retorna todos os documentos de uma consulta.</summary>
    [HttpGet("consulta/{consultaId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<DocumentoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterPorConsulta(Guid consultaId) =>
        Ok(await _service.ObterPorConsultaAsync(consultaId));

    /// <summary>Retorna um documento pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Gera um documento para uma consulta (seleciona a Factory pelo tipo — RN-024).</summary>
    [HttpPost("consulta/{consultaId:guid}")]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Gerar(Guid consultaId, [FromQuery] TipoDocumento tipo)
    {
        var doc = await _service.GerarAsync(consultaId, tipo);
        return CreatedAtAction(nameof(ObterPorId), new { id = doc.Id }, doc);
    }

    /// <summary>Assina um documento por nome digitado (RN-031 — MVP; nunca habilita dispensação de controlados — RN-091).</summary>
    [HttpPost("{id:guid}/assinar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Assinar(Guid id, [FromBody] AssinarDocumentoDto dto)
    {
        await _service.AssinarAsync(id, dto.NomeDigitado);
        return NoContent();
    }

    /// <summary>Cria uma versao corrigida de um documento (RN-032/033/034).</summary>
    [HttpPost("{id:guid}/correcao")]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Corrigir(Guid id, [FromBody] CorrecaoDocumentoRequest request) =>
        Ok(await _service.CorrigirAsync(id, request.NovosDados, request.Justificativa, request.CrmvSolicitante));
}

/// <summary>Payload para correcao de documento.</summary>
public sealed class CorrecaoDocumentoRequest
{
    public string NovosDados { get; set; } = string.Empty;
    public string? Justificativa { get; set; }
    public string CrmvSolicitante { get; set; } = string.Empty;
}
