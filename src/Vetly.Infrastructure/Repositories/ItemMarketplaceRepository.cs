using Microsoft.EntityFrameworkCore;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Marketplace;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório do catálogo do marketplace.</summary>
public class ItemMarketplaceRepository : RepositoryBase<ItemMarketplace>, IItemMarketplaceRepository
{
    public ItemMarketplaceRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<ResultadoPaginado<ItemMarketplace>> ObterPaginadoAsync(
        FiltroItemMarketplaceDto filtro,
        bool apenasAtivos,
        int? tetoDePontos,
        Paginacao paginacao,
        CriterioDeOrdenacao ordenacao)
    {
        // Leitura pura: sem rastreamento, a listagem nao carrega o change tracker
        IQueryable<ItemMarketplace> query = _dbSet.AsNoTracking();

        if (apenasAtivos)
            query = query.Where(i => i.Ativo);

        if (filtro.Categoria is { } categoria)
            query = query.Where(i => i.Categoria == categoria);

        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            // ToUpper dos dois lados: o Oracle compara texto com distincao de caixa, e
            // quem digita "ração" espera achar "Ração". Acento ainda conta — "racao"
            // nao acha "Ração"; ignorar acento pede indice linguistico no Oracle.
            var texto = filtro.Texto.Trim().ToUpperInvariant();
            query = query.Where(i => i.Nome.ToUpper().Contains(texto));
        }

        if (filtro.PontosMin is { } minimo)
            query = query.Where(i => i.CustoEmPontos >= minimo);

        if (filtro.PontosMax is { } maximo)
            query = query.Where(i => i.CustoEmPontos <= maximo);

        if (tetoDePontos is { } teto)
            query = query.Where(i => i.CustoEmPontos <= teto);

        var total = await query.CountAsync();

        var itens = await Ordenar(query, ordenacao)
            .Skip(paginacao.Deslocamento)
            .Take(paginacao.Tamanho)
            .ToListAsync();

        return new ResultadoPaginado<ItemMarketplace>(itens, total, paginacao);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<CategoriaItem, int>> ContarAtivosPorCategoriaAsync()
    {
        var contagem = await _dbSet
            .Where(i => i.Ativo)
            .GroupBy(i => i.Categoria)
            .Select(g => new { Categoria = g.Key, Quantidade = g.Count() })
            .ToListAsync();

        return contagem.ToDictionary(c => c.Categoria, c => c.Quantidade);
    }

    /// <summary>
    /// Traduz o campo da whitelist em expressao, num switch fechado, com desempate por
    /// <c>Id</c> para a paginacao ser estavel entre itens de mesmo custo ou nome.
    /// </summary>
    private static IQueryable<ItemMarketplace> Ordenar(IQueryable<ItemMarketplace> query, CriterioDeOrdenacao criterio)
    {
        var ordenada = (criterio.Campo, criterio.Descendente) switch
        {
            (CamposDeOrdenacao.Marketplace.CustoEmPontos, false) =>
                query.OrderBy(i => i.CustoEmPontos).ThenBy(i => i.Nome),
            (CamposDeOrdenacao.Marketplace.CustoEmPontos, true) =>
                query.OrderByDescending(i => i.CustoEmPontos).ThenBy(i => i.Nome),
            (CamposDeOrdenacao.Marketplace.CriadoEm, false) => query.OrderBy(i => i.CriadoEm),
            (CamposDeOrdenacao.Marketplace.CriadoEm, true) => query.OrderByDescending(i => i.CriadoEm),
            (_, false) => query.OrderBy(i => i.Nome),
            (_, true) => query.OrderByDescending(i => i.Nome)
        };

        return ordenada.ThenBy(i => i.Id);
    }
}
