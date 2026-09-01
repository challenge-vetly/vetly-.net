using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementacao do repositorio de <see cref="LembreteAgendado"/>.</summary>
public class LembreteRepository : RepositoryBase<LembreteAgendado>, ILembreteRepository
{
    public LembreteRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<LembreteAgendado>> ObterAtivosAteAsync(DateTime limite) =>
        await _context.Lembretes
            .Where(l => !l.TutorRespondeu && !l.AlertaEnviadoClinica && l.DataEvento <= limite)
            .OrderBy(l => l.DataEvento)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<LembreteAgendado>> ObterPendentesPorTutorAsync(Guid tutorId) =>
        await _dbSet
            .Where(l => l.TutorId == tutorId && !l.TutorRespondeu)
            .ToListAsync();
}
