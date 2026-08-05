using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de <see cref="Consulta"/> com filtros compostos.
/// </summary>
public class ConsultaRepository : RepositoryBase<Consulta>, IConsultaRepository
{
    public ConsultaRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<Consulta>> ObterPorVeterinarioAsync(
        Guid veterinarioId, DateTime? dataInicio = null, DateTime? dataFim = null)
    {
        var query = _dbSet.Where(c => c.VeterinarioId == veterinarioId);

        if (dataInicio.HasValue)
            query = query.Where(c => c.DataHora >= dataInicio.Value);

        if (dataFim.HasValue)
            query = query.Where(c => c.DataHora <= dataFim.Value);

        return await query.OrderBy(c => c.DataHora).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Consulta>> ObterPorAnimalAsync(Guid animalId) =>
        await _dbSet
            .Where(c => c.AnimalId == animalId)
            .OrderByDescending(c => c.DataHora)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Consulta>> ObterComFiltrosAsync(
        DateTime? dataInicio, DateTime? dataFim, Guid? veterinarioId, StatusConsulta? status)
    {
        // Constrói a query dinamicamente — apenas os filtros informados são aplicados
        var query = _dbSet.AsQueryable();

        if (dataInicio.HasValue)
            query = query.Where(c => c.DataHora >= dataInicio.Value);

        if (dataFim.HasValue)
            query = query.Where(c => c.DataHora <= dataFim.Value);

        if (veterinarioId.HasValue)
            query = query.Where(c => c.VeterinarioId == veterinarioId.Value);

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        return await query.OrderByDescending(c => c.DataHora).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> ExisteConsultaAsync(Guid veterinarioId, Guid animalId) =>
        await _dbSet.AnyAsync(c => c.VeterinarioId == veterinarioId && c.AnimalId == animalId);

    /// <inheritdoc/>
    public async Task<IEnumerable<Consulta>> ObterPorVeterinariosAsync(IEnumerable<Guid> veterinarioIds) =>
        await _dbSet
            .Where(c => veterinarioIds.Contains(c.VeterinarioId))
            .ToListAsync();
}
