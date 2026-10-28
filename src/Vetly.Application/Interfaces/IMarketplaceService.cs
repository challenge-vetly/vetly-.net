using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Fidelidade;
using Vetly.Application.DTOs.Marketplace;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Catálogo do marketplace e o ciclo do cupom: listar, resgatar por pontos e validar
/// no estabelecimento (RN-108 a RN-111).
/// </summary>
public interface IMarketplaceService
{
    /// <summary>
    /// Catálogo paginado. O Responsável vê só itens ativos; o Admin pode incluir os
    /// inativos. Ordenação na whitelist <see cref="CamposDeOrdenacao.Marketplace"/>.
    /// </summary>
    Task<ResultadoPaginado<ItemMarketplaceDto>> ListarAsync(
        FiltroItemMarketplaceDto filtro, Paginacao paginacao, Ordenacao? ordenacao = null);

    /// <summary>
    /// Detalhe do item com a situação do Responsável: saldo suficiente, quanto falta
    /// e o desconto que o cupom carregaria (RN-049/RN-051).
    /// </summary>
    Task<ItemMarketplaceDetalheDto> ObterPorIdAsync(Guid id);

    /// <summary>Cadastra um item no catálogo. Só o Admin.</summary>
    Task<ItemMarketplaceDto> CriarAsync(CriarItemMarketplaceDto dto);

    /// <summary>Altera um item do catálogo (e o reativa, se pedido). Só o Admin.</summary>
    Task<ItemMarketplaceDto> AtualizarAsync(Guid id, AtualizarItemMarketplaceDto dto);

    /// <summary>Tira o item do catálogo sem apagá-lo. Só o Admin.</summary>
    Task DesativarAsync(Guid id);

    /// <summary>
    /// Resgata um item do catálogo pelo Responsável do token: pontos em FIFO, unidade
    /// reservada e cupom emitido (RN-050/RN-053/RN-108/RN-109).
    /// </summary>
    Task<CupomDto> ResgatarAsync(ResgatarItemDto dto);

    /// <summary>
    /// Valida o QR do cupom no estabelecimento e o marca como usado (RN-110). Vencido
    /// é recusado (RN-053); já usado é conflito.
    /// </summary>
    Task<CupomDto> ValidarCupomAsync(ValidarCupomDto dto);

    /// <summary>Tabela de taxas de listagem por categoria — registrada, não cobrada (RN-111).</summary>
    Task<IReadOnlyList<TaxaDeListagemDto>> ObterTaxasDeListagemAsync();
}
