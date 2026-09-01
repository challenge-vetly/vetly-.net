using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Exame;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>Controller de exames. Gerencia solicitacao, registro de resultado e liberacao ao tutor.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExamesController : ControllerBase
{
    private readonly IExameService _service;

    public ExamesController(IExameService service) => _service = service;

    /// <summary>Retorna todos os exames.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ExameDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTodos() =>
        Ok(await _service.ObterTodosAsync());

    /// <summary>Retorna um exame pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Solicita um novo exame para um animal.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExameDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarExameDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Registra o resultado de um exame.</summary>
    [HttpPut("{id:guid}/resultado")]
    [ProducesResponseType(typeof(ExameDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarResultado(Guid id, [FromBody] RegistrarResultadoExameDto dto) =>
        Ok(await _service.RegistrarResultadoAsync(id, dto.Resultado, dto.MidiaIds));

    /// <summary>Libera o resultado do exame ao tutor (requer resultado registrado).</summary>
    [HttpPut("{id:guid}/liberar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> LiberarAoTutor(Guid id)
    {
        await _service.LiberarAoTutorAsync(id);
        return NoContent();
    }
}
