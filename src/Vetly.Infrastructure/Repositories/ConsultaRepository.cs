using Microsoft.EntityFrameworkCore;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Consulta;
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
    public async Task<ResultadoPaginado<Consulta>> ObterComFiltrosAsync(
        FiltroConsultaDto filtro, Paginacao paginacao)
    {
        // Constrói a query dinamicamente — apenas os filtros informados são aplicados
        var query = _dbSet.AsQueryable();

        if (filtro.DataInicio.HasValue)
            query = query.Where(c => c.DataHora >= filtro.DataInicio.Value);

        if (filtro.DataFim.HasValue)
            query = query.Where(c => c.DataHora <= filtro.DataFim.Value);

        if (filtro.VeterinarioId.HasValue)
            query = query.Where(c => c.VeterinarioId == filtro.VeterinarioId.Value);

        if (filtro.TutorId.HasValue)
            query = query.Where(c => c.TutorId == filtro.TutorId.Value);

        if (filtro.AnimalId.HasValue)
            query = query.Where(c => c.AnimalId == filtro.AnimalId.Value);

        if (filtro.Status.HasValue)
            query = query.Where(c => c.Status == filtro.Status.Value);

        // Filtro legado, mantido enquanto dura a dupla escrita do StatusConsulta
        if (filtro.Cancelada.HasValue)
            query = query.Where(c => c.Cancelada == filtro.Cancelada.Value);

        // A contagem roda sobre o filtro inteiro, antes do recorte da pagina
        var total = await query.CountAsync();

        var itens = await query
            .OrderByDescending(c => c.DataHora)
            .Skip(paginacao.Deslocamento)
            .Take(paginacao.Tamanho)
            .ToListAsync();

        return new ResultadoPaginado<Consulta>(itens, total, paginacao);
    }
}
