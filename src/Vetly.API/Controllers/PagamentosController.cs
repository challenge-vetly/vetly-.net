using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>Controller de pagamentos. Gerencia criacao e split financeiro via Strategy.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagamentosController : ControllerBase
{
    private readonly IPagamentoService _service;

    public PagamentosController(IPagamentoService service) => _service = service;

    /// <summary>
    /// Lista pagamentos, paginada (§2.3). Envelope { itens, total, pagina, tamanho }.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ResultadoPaginado<PagamentoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ObterTodos([FromQuery] Paginacao paginacao) =>
        Ok(await _service.ObterTodosAsync(paginacao));

    /// <summary>Retorna um pagamento pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PagamentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Registra um novo pagamento.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(PagamentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarPagamentoDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Processa o split financeiro aplicando a Strategy correta pela persona do veterinario.</summary>
    [HttpPost("{id:guid}/processar-split")]
    [ProducesResponseType(typeof(PagamentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ProcessarSplit(Guid id) =>
        Ok(await _service.ProcessarSplitAsync(id));
}
