using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>Controller de animais. Gerencia CRUD, historico de prontuarios e exames.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnimaisController : ControllerBase
{
    private readonly IAnimalService _service;

    public AnimaisController(IAnimalService service) => _service = service;

    /// <summary>Retorna todos os animais ativos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AnimalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTodos() =>
        Ok(await _service.ObterTodosAsync());

    /// <summary>Retorna um animal pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AnimalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Retorna o historico longitudinal de prontuarios de um animal (RN-010/083 — colmeia).</summary>
    [HttpGet("{id:guid}/prontuarios")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterProntuarios(Guid id) =>
        Ok(await _service.ObterHistoricoAsync(id));

    /// <summary>Retorna o log de acessos ao prontuario do animal — visivel ao responsavel (RN-086).</summary>
    [HttpGet("{id:guid}/log-acessos")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterLogAcessos(Guid id) =>
        Ok(await _service.ObterLogAcessosAsync(id));

    /// <summary>Retorna todos os exames de um animal.</summary>
    [HttpGet("{id:guid}/exames")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterExames(Guid id) =>
        Ok(await _service.ObterExamesAsync(id));

    /// <summary>Cadastra um novo animal vinculado a um responsavel.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AnimalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarAnimalDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza dados de um animal.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] CriarAnimalDto dto)
    {
        await _service.AtualizarAsync(id, dto);
        return NoContent();
    }

    /// <summary>Desativa um animal (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desativar(Guid id)
    {
        await _service.DesativarAsync(id);
        return NoContent();
    }

    /// <summary>Atualiza o peso do animal (RN-096.2).</summary>
    [HttpPut("{id:guid}/peso")]
    [ProducesResponseType(typeof(AnimalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarPeso(Guid id, [FromBody] AtualizarPesoDto dto) =>
        Ok(await _service.AtualizarPesoAsync(id, dto.PesoKg));

    /// <summary>Oculta um prontuário da visão de veterinários que não o produziram (RN-088).</summary>
    [HttpPost("{id:guid}/ocultar-registro")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> OcultarRegistro(Guid id, [FromBody] OcultarRegistroDto dto)
    {
        await _service.OcultarRegistroAsync(id, dto.ProntuarioId);
        return Ok();
    }

    /// <summary>Reexibe um prontuário previamente ocultado.</summary>
    [HttpDelete("{id:guid}/ocultar-registro/{prontuarioId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReexibirRegistro(Guid id, Guid prontuarioId)
    {
        await _service.ReexibirRegistroAsync(id, prontuarioId);
        return Ok();
    }
}
