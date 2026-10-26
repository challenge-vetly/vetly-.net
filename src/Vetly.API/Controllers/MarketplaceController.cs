using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.API.Filters;
using Vetly.API.Hateoas;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Fidelidade;
using Vetly.Application.DTOs.Marketplace;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Marketplace: catalogo de itens trocaveis por pontos e o ciclo do cupom
/// (RN-108 a RN-111).
///
/// O catalogo mora aqui, e nao mais no app: o resgate envia so o id do item, e nome,
/// categoria e custo em pontos sao decididos pelo servidor (RN-108).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MarketplaceController : ControllerBase
{
    private readonly IMarketplaceService _service;
    private readonly RespostaHateoas _hateoas;

    public MarketplaceController(IMarketplaceService service, RespostaHateoas hateoas)
    {
        _service = service;
        _hateoas = hateoas;
    }

    /// <summary>Catalogo paginado, com filtros e ordenacao (RN-108).</summary>
    /// <remarks>
    /// Filtros: <c>categoria</c> (Alimentacao, Medicamentos, Higiene), <c>texto</c>
    /// (trecho do nome), <c>pontosMin</c>/<c>pontosMax</c> e <c>apenasAcessiveis</c>
    /// (so o que o saldo do Responsavel cobre, calculado no servidor).
    /// <c>incluirInativos</c> so vale para o Admin.
    ///
    /// Ordenacao: <c>ordenarPor=nome|custoEmPontos|criadoEm</c> e
    /// <c>direcao=asc|desc</c>; padrao <c>nome asc</c>. Com
    /// <c>Accept: application/hal+json</c> ou <c>?hateoas=true</c>, cada item traz os
    /// links do que da para fazer (resgatar so aparece para o Responsavel e com o item
    /// disponivel).
    /// </remarks>
    [HttpGet("itens")]
    [ProducesResponseType(typeof(ResultadoPaginado<ItemMarketplaceDto>), StatusCodes.Status200OK)]
    [RespostaHal(typeof(RecursoPaginado<ItemMarketplaceDto>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListarItens(
        [FromQuery] FiltroItemMarketplaceDto filtro,
        [FromQuery] Paginacao paginacao,
        [FromQuery] Ordenacao ordenacao) =>
        _hateoas.Ok(await _service.ListarAsync(filtro, paginacao, ordenacao));

    /// <summary>Detalhe do item, com a situacao do Responsavel (RN-049/RN-051).</summary>
    /// <remarks>
    /// Para o Responsavel, vem <c>saldoAtual</c>, <c>saldoSuficiente</c> e
    /// <c>pontosQueFaltam</c>, alem do desconto que o cupom carregaria e da divisao do
    /// custo entre Vetly e prestador. Item desativado devolve 404 para quem nao e Admin.
    /// </remarks>
    [HttpGet("itens/{id:guid}")]
    [ProducesResponseType(typeof(ItemMarketplaceDetalheDto), StatusCodes.Status200OK)]
    [RespostaHal(typeof(Recurso<ItemMarketplaceDetalheDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterItem(Guid id) =>
        _hateoas.Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Cadastra um item no catalogo (Admin).</summary>
    [HttpPost("itens")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(ItemMarketplaceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CriarItem([FromBody] CriarItemMarketplaceDto dto)
    {
        var item = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterItem), new { id = item.Id }, item);
    }

    /// <summary>Altera um item do catalogo (Admin).</summary>
    /// <remarks>
    /// <c>ativo: true</c> reativa um item desativado; omitido, mantem como esta.
    /// Alterar o custo nao mexe em cupons ja emitidos: cada cupom guarda os pontos que
    /// debitou.
    /// </remarks>
    [HttpPut("itens/{id:guid}")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(ItemMarketplaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AtualizarItem(Guid id, [FromBody] AtualizarItemMarketplaceDto dto) =>
        Ok(await _service.AtualizarAsync(id, dto));

    /// <summary>Tira o item do catalogo, sem apaga-lo (Admin).</summary>
    /// <remarks>
    /// Desativa: os cupons ja emitidos apontam para o item, e um item apagado deixaria
    /// o historico sem referencia. Idempotente — desativar de novo tambem devolve 204.
    /// </remarks>
    [HttpDelete("itens/{id:guid}")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesativarItem(Guid id)
    {
        await _service.DesativarAsync(id);
        return NoContent();
    }

    /// <summary>Resgata o item por pontos e emite o cupom (RN-050/RN-053/RN-108/RN-109).</summary>
    /// <remarks>
    /// Sem corpo: o item e o da rota, e o custo vem do catalogo. Os pontos saem em
    /// FIFO, uma unidade do estoque e reservada e o cupom vale 30 dias. Vencido, os
    /// pontos nao voltam (RN-053), mas a unidade volta ao estoque (RN-109).
    ///
    /// Item inativo ou sem estoque devolve 422 com codigo <c>RN-108</c>; saldo
    /// insuficiente, 422 com <c>RN-050</c>; dois resgates simultaneos da ultima
    /// unidade, 409 no segundo.
    /// </remarks>
    [HttpPost("itens/{id:guid}/resgatar")]
    [Authorize(Policy = "ApenasTutor")]
    [Idempotente]
    [ProducesResponseType(typeof(CupomDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Resgatar(Guid id)
    {
        var cupom = await _service.ResgatarAsync(new ResgatarItemDto { ItemId = id });

        return CreatedAtAction(
            nameof(FidelidadeController.ObterCupom), "Fidelidade", new { id = cupom.Id }, cupom);
    }

    /// <summary>Valida o QR do cupom no estabelecimento (RN-110).</summary>
    /// <remarks>
    /// O cupom vigente passa a <c>Resgatado</c>. Vencido devolve 422 com
    /// <c>RN-053</c> (e a unidade volta ao estoque); ja utilizado devolve 409 com
    /// <c>MARKETPLACE-001</c>; codigo inexistente, 404. O codigo e aceito em qualquer
    /// caixa, porque pode ser digitado a mao.
    /// </remarks>
    [HttpPost("cupons/validar")]
    [Authorize(Policy = "VeterinarioOuAdmin")]
    [Idempotente]
    [ProducesResponseType(typeof(CupomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ValidarCupom([FromBody] ValidarCupomDto dto) =>
        Ok(await _service.ValidarCupomAsync(dto));

    /// <summary>Taxas de listagem por categoria — registradas, nao cobradas (RN-111).</summary>
    [HttpGet("taxas-de-listagem")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(IReadOnlyList<TaxaDeListagemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterTaxasDeListagem() =>
        Ok(await _service.ObterTaxasDeListagemAsync());
}
