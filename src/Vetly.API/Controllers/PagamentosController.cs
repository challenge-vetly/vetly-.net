using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.API.Filters;
using Vetly.API.Hateoas;
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
    private readonly RespostaHateoas _hateoas;

    public PagamentosController(IPagamentoService service, RespostaHateoas hateoas)
    {
        _service = service;
        _hateoas = hateoas;
    }

    /// <summary>
    /// Lista pagamentos, paginada (§2.3). Envelope { itens, total, pagina, tamanho }.
    /// </summary>
    /// <remarks>
    /// Escopo do token (RN-106): o Responsavel ve os proprios, o Admin ve todos.
    /// Filtros: <c>status</c>, <c>tipo</c> e periodo (<c>de</c>/<c>ate</c>). Ordenacao:
    /// <c>ordenarPor=momento|valor|status</c> e <c>direcao=asc|desc</c>; padrao
    /// <c>momento desc</c>. Com <c>Accept: application/hal+json</c> ou
    /// <c>?hateoas=true</c>, cada pagamento vem com os proprios links.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ResultadoPaginado<PagamentoDto>), StatusCodes.Status200OK)]
    [RespostaHal(typeof(RecursoPaginado<PagamentoDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ObterTodos(
        [FromQuery] Paginacao paginacao,
        [FromQuery] FiltroPagamentoDto filtro,
        [FromQuery] Ordenacao ordenacao) =>
        _hateoas.Ok(await _service.ObterTodosAsync(paginacao, filtro, ordenacao));

    /// <summary>Retorna um pagamento pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PagamentoDto), StatusCodes.Status200OK)]
    [RespostaHal(typeof(Recurso<PagamentoDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        _hateoas.Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Registra um novo pagamento.</summary>
    /// <summary>
    /// Cria a cobranca com o split ja apurado pela Vetly (RN-070) e devolve as
    /// instrucoes de pagamento.
    /// </summary>
    /// <remarks>
    /// Responde 202: o pagamento fica PENDENTE. A confirmacao chega pelo webhook do
    /// provedor, nunca por esta resposta — e o que mantem o fluxo pronto para um
    /// gateway real (RN-006, vetly-tech §7.5). Use
    /// <c>GET /api/pagamentos/{id}/status</c> para acompanhar.
    /// </remarks>
    [HttpPost]
    [Idempotente]
    [ProducesResponseType(typeof(CobrancaCriadaRespostaDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CriarCobranca([FromBody] CriarPagamentoDto dto)
    {
        var cobranca = await _service.CriarCobrancaAsync(dto);
        return Accepted(cobranca);
    }

    /// <summary>
    /// Status da cobranca e da consulta vinculada. E o polling do app durante o
    /// checkout (RN-006).
    /// </summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(StatusDaCobrancaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterStatus(Guid id) =>
        Ok(await _service.ObterStatusAsync(id));

    /// <summary>
    /// Reapura o split de um pagamento (RN-070).
    /// </summary>
    /// <remarks>
    /// O split ja e apurado automaticamente na criacao da cobranca. Esta rota fica
    /// como ferramenta de correcao, para o caso de a consulta ou o plano terem mudado
    /// depois — nao e parte do fluxo normal.
    /// </remarks>
    [HttpPost("{id:guid}/processar-split")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(PagamentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ProcessarSplit(Guid id) =>
        Ok(await _service.ProcessarSplitAsync(id));
}
