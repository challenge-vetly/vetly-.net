using Microsoft.EntityFrameworkCore;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="Pagamento"/>.</summary>
public class PagamentoRepository : RepositoryBase<Pagamento>, IPagamentoRepository
{
    public PagamentoRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<Pagamento>> ObterPorTutorAsync(Guid tutorId) =>
        await _dbSet
            .Where(p => p.TutorId == tutorId)
            .OrderByDescending(p => p.Momento)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<ResultadoPaginado<Pagamento>> ObterPaginadoAsync(
        Paginacao paginacao,
        Guid? tutorId = null,
        FiltroPagamentoDto? filtro = null,
        CriterioDeOrdenacao? ordenacao = null)
    {
        IQueryable<Pagamento> query = tutorId is null ? _dbSet : _dbSet.Where(p => p.TutorId == tutorId.Value);

        // Apenas os filtros informados entram na query
        if (filtro?.Status is { } status)
            query = query.Where(p => p.StatusPagamento == status);

        if (filtro?.Tipo is { } tipo)
            query = query.Where(p => p.Tipo == tipo);

        if (filtro?.De is { } de)
            query = query.Where(p => p.Momento >= de);

        if (filtro?.Ate is { } ate)
            query = query.Where(p => p.Momento <= ate);

        var total = await query.CountAsync();

        // Ordena antes de recortar, pelo mesmo motivo da listagem de consultas
        var itens = await Ordenar(query, ordenacao ?? PadraoPagamentos)
            .Skip(paginacao.Deslocamento)
            .Take(paginacao.Tamanho)
            .ToListAsync();

        return new ResultadoPaginado<Pagamento>(itens, total, paginacao);
    }

    /// <summary>Momento decrescente: o que a listagem sempre devolveu (§2.3).</summary>
    private static readonly CriterioDeOrdenacao PadraoPagamentos =
        new(CamposDeOrdenacao.Pagamentos.Momento, Descendente: true);

    /// <summary>
    /// Traduz o campo da whitelist em expressao, num switch fechado, com desempate
    /// por <c>Id</c> para a paginacao nao repetir nem pular registros empatados.
    /// </summary>
    private static IQueryable<Pagamento> Ordenar(IQueryable<Pagamento> query, CriterioDeOrdenacao criterio)
    {
        var ordenada = (criterio.Campo, criterio.Descendente) switch
        {
            (CamposDeOrdenacao.Pagamentos.Valor, false) =>
                query.OrderBy(p => p.Valor).ThenByDescending(p => p.Momento),
            (CamposDeOrdenacao.Pagamentos.Valor, true) =>
                query.OrderByDescending(p => p.Valor).ThenByDescending(p => p.Momento),
            (CamposDeOrdenacao.Pagamentos.Status, false) =>
                query.OrderBy(p => p.StatusPagamento).ThenByDescending(p => p.Momento),
            (CamposDeOrdenacao.Pagamentos.Status, true) =>
                query.OrderByDescending(p => p.StatusPagamento).ThenByDescending(p => p.Momento),
            (_, false) => query.OrderBy(p => p.Momento),
            (_, true) => query.OrderByDescending(p => p.Momento)
        };

        return ordenada.ThenBy(p => p.Id);
    }

    /// <inheritdoc/>
    public async Task<Pagamento?> ObterPorReferenciaExternaAsync(string referenciaExterna) =>
        await _dbSet.FirstOrDefaultAsync(p => p.ReferenciaExterna == referenciaExterna);

    /// <inheritdoc/>
    public async Task<Pagamento?> ObterPorConsultaAsync(Guid consultaId) =>
        await _dbSet
            .FirstOrDefaultAsync(p => p.ConsultaId == consultaId);

    /// <inheritdoc/>
    public async Task<IEnumerable<Pagamento>> ObterConfirmadosNoPeriodoAsync(DateTime inicio, DateTime fim) =>
        // Cobranca pendente ou recusada nao entra em fechamento: dinheiro que nao
        // entrou nao se repassa (RN-071).
        await _dbSet
            .Where(p => p.StatusPagamento == StatusPagamento.Confirmado
                        && p.Momento >= inicio
                        && p.Momento <= fim)
            .OrderBy(p => p.Momento)
            .ToListAsync();
}
