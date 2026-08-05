using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="LogAcessoProntuario"/>.</summary>
public class LogAcessoProntuarioRepository : RepositoryBase<LogAcessoProntuario>, ILogAcessoProntuarioRepository
{
    public LogAcessoProntuarioRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<LogAcessoProntuario>> ObterPorAnimalAsync(Guid animalId) =>
        await _dbSet
            .Where(l => l.AnimalId == animalId)
            .OrderByDescending(l => l.DataHora)
            .ToListAsync();
}
