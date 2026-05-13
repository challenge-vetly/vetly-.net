using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="Tutor"/>.</summary>
public class TutorRepository : RepositoryBase<Tutor>, ITutorRepository
{
    public TutorRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<Tutor?> ObterPorEmailAsync(string email) =>
        await _dbSet.FirstOrDefaultAsync(t => t.Email == email.ToLowerInvariant());

    /// <inheritdoc/>
    public async Task<IEnumerable<Tutor>> ObterAtivosAsync() =>
        await _dbSet.Where(t => t.Ativo).ToListAsync();
}
