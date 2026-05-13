using Microsoft.EntityFrameworkCore;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="Internacao"/>.</summary>
public class InternacaoRepository : RepositoryBase<Internacao>, IInternacaoRepository
{
    public InternacaoRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<Internacao>> ObterPorAnimalAsync(Guid animalId) =>
        await _dbSet
            .Where(i => i.AnimalId == animalId)
            .OrderByDescending(i => i.DataAbertura)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<Internacao?> ObterAtivaDoAnimalAsync(Guid animalId) =>
        await _dbSet
            .FirstOrDefaultAsync(i => i.AnimalId == animalId && i.DataAlta == null);
}
