using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="ObrigacaoDoPet"/>.</summary>
public class ObrigacaoDoPetRepository : RepositoryBase<ObrigacaoDoPet>, IObrigacaoDoPetRepository
{
    public ObrigacaoDoPetRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<ObrigacaoDoPet>> ObterPorAnimalAsync(Guid animalId) =>
        await _dbSet
            .Where(o => o.AnimalId == animalId)
            .OrderBy(o => o.DataLimite)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<bool> ExisteCalendarioAsync(Guid animalId) =>
        await _dbSet.AnyAsync(o => o.AnimalId == animalId);

    /// <inheritdoc/>
    public async Task<ObrigacaoDoPet?> ObterPendenteMaisProximaAsync(Guid animalId, TipoObrigacao tipo) =>
        await _dbSet
            .Where(o => o.AnimalId == animalId && o.Tipo == tipo && o.Status == StatusObrigacao.Pendente)
            .OrderBy(o => o.DataLimite)
            .FirstOrDefaultAsync();
}
