using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
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
        DateTime? dataInicio, DateTime? dataFim, Guid? veterinarioId, bool? cancelada)
    {
        // Constrói a query dinamicamente — apenas os filtros informados são aplicados
        var query = _dbSet.AsQueryable();

        if (dataInicio.HasValue)
            query = query.Where(c => c.DataHora >= dataInicio.Value);

        if (dataFim.HasValue)
            query = query.Where(c => c.DataHora <= dataFim.Value);

        if (veterinarioId.HasValue)
            query = query.Where(c => c.VeterinarioId == veterinarioId.Value);

        if (cancelada.HasValue)
            query = query.Where(c => c.Cancelada == cancelada.Value);

        return await query.OrderByDescending(c => c.DataHora).ToListAsync();
    }
}
