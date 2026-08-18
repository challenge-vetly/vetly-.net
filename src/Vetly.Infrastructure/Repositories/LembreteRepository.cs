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
    public async Task<IEnumerable<LembreteAgendado>> ObterPendentesPorResponsavelAsync(Guid responsavelId) =>
        await _dbSet
            .Where(l => l.ResponsavelId == responsavelId && !l.ResponsavelRespondeu)
            .ToListAsync();
}
