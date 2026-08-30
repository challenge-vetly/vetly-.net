using Microsoft.EntityFrameworkCore;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
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
    public async Task<ResultadoPaginado<Pagamento>> ObterPaginadoAsync(Paginacao paginacao)
    {
        var total = await _dbSet.CountAsync();

        var itens = await _dbSet
            .OrderByDescending(p => p.Momento)
            .Skip(paginacao.Deslocamento)
            .Take(paginacao.Tamanho)
            .ToListAsync();

        return new ResultadoPaginado<Pagamento>(itens, total, paginacao);
    }

    /// <inheritdoc/>
    public async Task<Pagamento?> ObterPorConsultaAsync(Guid consultaId) =>
        await _dbSet
            .FirstOrDefaultAsync(p => p.ConsultaId == consultaId);
}
