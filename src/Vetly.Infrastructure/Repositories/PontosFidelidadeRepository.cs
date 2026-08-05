using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="PontosFidelidade"/>.</summary>
public class PontosFidelidadeRepository : RepositoryBase<PontosFidelidade>, IPontosFidelidadeRepository
{
    public PontosFidelidadeRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<PontosFidelidade>> ObterPorResponsavelAsync(Guid responsavelId) =>
        await _dbSet
            .Where(p => p.ResponsavelId == responsavelId)
            .OrderByDescending(p => p.Data)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<PontosFidelidade?> ObterPorConsultaAsync(Guid consultaId) =>
        await _dbSet.FirstOrDefaultAsync(p => p.ConsultaId == consultaId);
}
