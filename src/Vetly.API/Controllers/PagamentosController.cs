using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>Retorna todos os pagamentos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PagamentoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTodos() =>
        Ok(await _service.ObterTodosAsync());

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

    /// <summary>
    /// Simula o pagamento de uma consulta: sempre retorna sucesso, calcula a comissão pelo
    /// plano do veterinário (RN-089) e confirma a consulta (RN-037/058).
    /// </summary>
    [HttpPost("simular")]
    [ProducesResponseType(typeof(SimularPagamentoResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Simular([FromBody] SimularPagamentoDto dto)
    {
        var resultado = await _service.ProcessarSimuladoAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Id }, resultado);
    }
}
