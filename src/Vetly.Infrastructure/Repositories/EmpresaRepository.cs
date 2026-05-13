using Microsoft.EntityFrameworkCore;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="Empresa"/>.</summary>
public class EmpresaRepository : RepositoryBase<Empresa>, IEmpresaRepository
{
    public EmpresaRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<Empresa>> ObterAtivasAsync() =>
        await _dbSet.Where(e => e.Ativa).ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Empresa>> ObterPorAdministradorAsync(Guid veterinarioId) =>
        await _dbSet
            .Where(e => e.AdministradorId == veterinarioId && e.Ativa)
            .ToListAsync();
}
