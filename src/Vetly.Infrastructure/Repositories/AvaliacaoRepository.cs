using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="Avaliacao"/>.</summary>
public class AvaliacaoRepository : RepositoryBase<Avaliacao>, IAvaliacaoRepository
{
    public AvaliacaoRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<Avaliacao?> ObterPorConsultaAsync(Guid consultaId) =>
        await _dbSet.FirstOrDefaultAsync(a => a.ConsultaId == consultaId);

    /// <inheritdoc/>
    public async Task<IEnumerable<Avaliacao>> ObterValidasPorVeterinarioAsync(Guid veterinarioId) =>
        await _dbSet
            .Where(a => a.VeterinarioId == veterinarioId && !a.Invalidada)
            .OrderByDescending(a => a.Data)
            .ToListAsync();
}
