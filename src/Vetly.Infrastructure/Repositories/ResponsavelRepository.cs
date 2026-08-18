using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="Responsavel"/>.</summary>
public class ResponsavelRepository : RepositoryBase<Responsavel>, IResponsavelRepository
{
    public ResponsavelRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<Responsavel?> ObterPorEmailAsync(string email) =>
        await _dbSet.FirstOrDefaultAsync(t => t.Email == email.ToLowerInvariant());

    /// <inheritdoc/>
    public async Task<IEnumerable<Responsavel>> ObterAtivosAsync() =>
        await _dbSet.Where(t => t.Ativo).ToListAsync();
}
