using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="Dispositivo"/>.</summary>
public class DispositivoRepository : RepositoryBase<Dispositivo>, IDispositivoRepository
{
    public DispositivoRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<Dispositivo>> ObterAtivosDoTutorAsync(Guid tutorId) =>
        await _dbSet
            .Where(d => d.TutorId == tutorId && d.Ativo)
            .OrderByDescending(d => d.UltimoUsoEm)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<Dispositivo?> ObterPorPushTokenAsync(string pushToken) =>
        await _dbSet.FirstOrDefaultAsync(d => d.PushToken == pushToken);
}
