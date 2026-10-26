using Vetly.API.Controllers;
using Vetly.Application.DTOs.Carteira;

namespace Vetly.API.Hateoas;

/// <summary>
/// Links de um lançamento da carteira para a sua origem: o pagamento (e a consulta
/// paga), ou o cupom e o item resgatado (RN-115). É o que deixa a linha do tempo ser
/// navegável sem o app saber de qual tabela cada linha saiu.
/// </summary>
public sealed class LinksDoLancamento : ILinksDoRecurso<LancamentoDoHistoricoDto>
{
    private readonly ConstrutorDeLinks _links;

    /// <summary>Cria o fornecedor de links do histórico da carteira.</summary>
    public LinksDoLancamento(ConstrutorDeLinks links) => _links = links;

    /// <inheritdoc/>
    public IEnumerable<Link> Para(LancamentoDoHistoricoDto lancamento)
    {
        if (lancamento.PagamentoId is { } pagamentoId)
            yield return _links.Para<PagamentosController>(
                "pagamento", HttpMethods.Get, nameof(PagamentosController.ObterPorId), new { id = pagamentoId });

        if (lancamento.ConsultaId is { } consultaId)
            yield return _links.Para<ConsultasController>(
                "consulta", HttpMethods.Get, nameof(ConsultasController.ObterPorId), new { id = consultaId });

        if (lancamento.CupomId is { } cupomId)
            yield return _links.Para<FidelidadeController>(
                "cupom", HttpMethods.Get, nameof(FidelidadeController.ObterCupom), new { id = cupomId });

        if (lancamento.ItemMarketplaceId is { } itemId)
            yield return _links.Para<MarketplaceController>(
                "item", HttpMethods.Get, nameof(MarketplaceController.ObterItem), new { id = itemId });
    }
}
