using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="ConcessaoAcessoProntuario"/>.</summary>
public class ConcessaoAcessoProntuarioRepository : RepositoryBase<ConcessaoAcessoProntuario>, IConcessaoAcessoProntuarioRepository
{
    public ConcessaoAcessoProntuarioRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<ConcessaoAcessoProntuario?> ObterAtivaAsync(Guid veterinarioId, Guid animalId, DateTime agora) =>
        await _dbSet
            .Where(c => c.VeterinarioId == veterinarioId && c.AnimalId == animalId
                && !c.Revogada && c.ExpiraEm >= agora)
            .OrderByDescending(c => c.ExpiraEm)
            .FirstOrDefaultAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<ConcessaoAcessoProntuario>> ObterAtivasPorVeterinarioAsync(Guid veterinarioId, DateTime agora) =>
        await _dbSet
            .Where(c => c.VeterinarioId == veterinarioId && !c.Revogada && c.ExpiraEm >= agora)
            .OrderByDescending(c => c.ExpiraEm)
            .ToListAsync();
}
