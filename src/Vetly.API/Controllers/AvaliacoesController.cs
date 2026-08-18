using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Avaliacao;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Controller de avaliações. Publicação acontece via
/// <c>POST /api/consultas/{id}/avaliacao</c> (ConsultasController); aqui ficam as
/// operações sobre uma avaliação já existente (RN-079/080/082).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AvaliacoesController : ControllerBase
{
    private readonly IAvaliacaoService _service;

    public AvaliacoesController(IAvaliacaoService service)
    {
        _service = service;
    }

    /// <summary>Retorna uma avaliação pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AvaliacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Edita a avaliação, só permitido dentro de 48h da publicação (RN-082).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AvaliacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Editar(Guid id, [FromBody] EditarAvaliacaoDto dto) =>
        Ok(await _service.EditarAsync(id, dto));

    /// <summary>Registra a resposta pública do veterinário — só uma por avaliação (RN-079).</summary>
    [HttpPost("{id:guid}/resposta")]
    [ProducesResponseType(typeof(AvaliacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Responder(Guid id, [FromBody] ResponderAvaliacaoDto dto) =>
        Ok(await _service.ResponderAsync(id, dto));

    /// <summary>
    /// Aplica moderação ao comentário (RN-080). Restrito a Admins — a nota nunca é
    /// alterada por este endpoint, só a visibilidade do texto.
    /// </summary>
    [HttpPost("{id:guid}/moderar")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(AvaliacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Moderar(Guid id, [FromBody] ModerarAvaliacaoDto dto) =>
        Ok(await _service.ModerarAsync(id, dto));
}
