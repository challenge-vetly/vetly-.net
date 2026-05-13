using Microsoft.EntityFrameworkCore;
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
    public async Task<Pagamento?> ObterPorConsultaAsync(Guid consultaId) =>
        await _dbSet
            .FirstOrDefaultAsync(p => p.ConsultaId == consultaId);
}
