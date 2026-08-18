using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="Pagamento"/>.</summary>
public class PagamentoRepository : RepositoryBase<Pagamento>, IPagamentoRepository
{
    public PagamentoRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<Pagamento>> ObterPorResponsavelAsync(Guid responsavelId) =>
        await _dbSet
            .Where(p => p.ResponsavelId == responsavelId)
            .OrderByDescending(p => p.Momento)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<Pagamento?> ObterPorConsultaAsync(Guid consultaId) =>
        await _dbSet
            .FirstOrDefaultAsync(p => p.ConsultaId == consultaId);

    /// <inheritdoc/>
    public async Task<IEnumerable<Pagamento>> ObterPorVeterinariosAsync(IEnumerable<Guid> veterinarioIds) =>
        await _dbSet
            .Join(_context.Consultas, p => p.ConsultaId, c => c.Id, (p, c) => new { Pagamento = p, c.VeterinarioId })
            .Where(x => veterinarioIds.Contains(x.VeterinarioId))
            .Select(x => x.Pagamento)
            .ToListAsync();
}
