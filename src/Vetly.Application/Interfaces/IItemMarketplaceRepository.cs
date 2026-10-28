using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Marketplace;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Repositório do catálogo do marketplace (RN-108 a RN-111).</summary>
public interface IItemMarketplaceRepository : IRepositoryBase<ItemMarketplace>
{
    /// <summary>
    /// Página do catálogo com filtros e ordenação aplicados no banco, antes do recorte.
    /// </summary>
    /// <param name="filtro">Categoria, texto e faixa de pontos.</param>
    /// <param name="apenasAtivos">Falso só para o Admin que pediu os inativos.</param>
    /// <param name="tetoDePontos">
    /// Saldo do Responsável quando ele pediu "só o que consigo resgatar"; nulo não
    /// limita. Vem separado do <c>pontosMax</c> do filtro porque é o servidor que o
    /// calcula — o cliente não informa o próprio saldo.
    /// </param>
    /// <param name="paginacao">Página pedida.</param>
    /// <param name="ordenacao">Critério já resolvido contra <see cref="CamposDeOrdenacao.Marketplace"/>.</param>
    Task<ResultadoPaginado<ItemMarketplace>> ObterPaginadoAsync(
        FiltroItemMarketplaceDto filtro,
        bool apenasAtivos,
        int? tetoDePontos,
        Paginacao paginacao,
        CriterioDeOrdenacao ordenacao);

    /// <summary>Quantos itens ativos há em cada categoria, para a tabela de taxas (RN-111).</summary>
    Task<IReadOnlyDictionary<CategoriaItem, int>> ContarAtivosPorCategoriaAsync();
}
