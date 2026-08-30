using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.ListaEspera;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Lista de espera por horario (RN-004/RN-037).
///
/// E a terceira saida da RN-004 quando nao ha horario disponivel: em vez de perder a
/// demanda, a plataforma guarda a intencao e avisa quando abrir vaga.
/// </summary>
[ApiController]
[Route("api/lista-espera")]
[Authorize]
public class ListaEsperaController : ControllerBase
{
    private readonly IListaEsperaService _service;

    public ListaEsperaController(IListaEsperaService service) => _service = service;

    /// <summary>Pedidos do proprio Responsavel na lista de espera.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ItemListaEsperaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ObterMinhaLista()
    {
        var tutorId = User.FindFirstValue("tutorId");

        if (!Guid.TryParse(tutorId, out var id))
            return Unauthorized();

        return Ok(await _service.ObterDoTutorAsync(id));
    }

    /// <summary>Entra na lista de espera de um veterinario (RN-004).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ItemListaEsperaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Entrar([FromBody] EntrarNaListaDto dto)
    {
        var item = await _service.EntrarAsync(dto);
        return Created(string.Empty, item);
    }

    /// <summary>Sai da lista de espera.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sair(Guid id)
    {
        await _service.SairAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Aceita a vaga oferecida e segue direto para o checkout (RN-037).
    /// Prioridade vencida devolve 409 — a vaga ja passou ao proximo da fila.
    /// </summary>
    [HttpPost("{id:guid}/confirmar")]
    [ProducesResponseType(typeof(CheckoutCriadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmarVaga(Guid id, [FromBody] ConfirmarVagaDto dto) =>
        Ok(await _service.ConfirmarVagaAsync(id, dto.ServicoId));
}
