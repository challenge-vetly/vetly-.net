using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="RefreshToken"/>.</summary>
public class RefreshTokenRepository : RepositoryBase<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<RefreshToken?> ObterPorHashAsync(string hash) =>
        await _dbSet.FirstOrDefaultAsync(t => t.Hash == hash);

    /// <inheritdoc/>
    public async Task<int> RevogarTodosDoUsuarioAsync(Guid usuarioId, DateTime quando)
    {
        var ativos = await _dbSet
            .Where(t => t.UsuarioId == usuarioId && !t.Revogado)
            .ToListAsync();

        foreach (var token in ativos)
            token.Revogar(quando);

        return ativos.Count;
    }
}
