using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de <see cref="Veterinario"/> com queries específicas do domínio.
/// </summary>
public class VeterinarioRepository : RepositoryBase<Veterinario>, IVeterinarioRepository
{
    public VeterinarioRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<Veterinario?> ObterPorCrmvAsync(string crmv) =>
        await _dbSet
            .FirstOrDefaultAsync(v => v.Crmv.Valor == crmv.ToUpperInvariant());

    /// <inheritdoc/>
    public async Task<IEnumerable<Veterinario>> ObterPorUfAsync(string uf) =>
        await _dbSet
            .Where(v => v.Ativo && v.UfAtuacao == uf.ToUpperInvariant())
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Consulta>> ObterAgendaFuturaAsync(Guid veterinarioId) =>
        await _context.Consultas
            .Where(c => c.VeterinarioId == veterinarioId
                     && c.DataHora > DateTime.UtcNow
                     && !c.Cancelada)
            .OrderBy(c => c.DataHora)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Veterinario>> ObterAtivosAsync() =>
        await _dbSet
            .Where(v => v.Ativo)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Veterinario>> ObterPorEmpresaAsync(Guid empresaId) =>
        await _dbSet
            .Where(v => v.EmpresaId == empresaId && v.Ativo)
            .ToListAsync();
}
