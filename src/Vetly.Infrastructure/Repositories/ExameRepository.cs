using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="Exame"/>.</summary>
public class ExameRepository : RepositoryBase<Exame>, IExameRepository
{
    public ExameRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<Exame>> ObterPorAnimalAsync(Guid animalId) =>
        await _dbSet
            .Where(e => e.AnimalId == animalId)
            .OrderByDescending(e => e.DataSolicitacao)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Exame>> ObterPorVeterinarioAsync(Guid veterinarioId) =>
        await _dbSet
            .Where(e => e.VeterinarioId == veterinarioId)
            .OrderByDescending(e => e.DataSolicitacao)
            .ToListAsync();
}
