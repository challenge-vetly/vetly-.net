using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="RegistroOcultado"/>.</summary>
public class RegistroOcultadoRepository : RepositoryBase<RegistroOcultado>, IRegistroOcultadoRepository
{
    public RegistroOcultadoRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<RegistroOcultado>> ObterPorAnimalAsync(Guid animalId) =>
        await _dbSet.Where(r => r.AnimalId == animalId).ToListAsync();

    /// <inheritdoc/>
    public async Task<RegistroOcultado?> ObterAsync(Guid animalId, Guid prontuarioId) =>
        await _dbSet.FirstOrDefaultAsync(r => r.AnimalId == animalId && r.ProntuarioId == prontuarioId);
}
