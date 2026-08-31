using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="Documento"/>.</summary>
public class DocumentoRepository : RepositoryBase<Documento>, IDocumentoRepository
{
    public DocumentoRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<Documento>> ObterPorConsultaAsync(Guid consultaId) =>
        await _dbSet
            .Where(d => d.ConsultaId == consultaId)
            .OrderBy(d => d.TipoDocumento)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Documento>> ObterPorInternacaoAsync(Guid internacaoId) =>
        await _dbSet
            .Where(d => d.InternacaoId == internacaoId)
            .OrderBy(d => d.DataGeracao)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<Documento?> ObterPorConsultaETipoAsync(Guid consultaId, TipoDocumento tipo) =>
        await _dbSet
            .FirstOrDefaultAsync(d => d.ConsultaId == consultaId && d.TipoDocumento == tipo);

    /// <inheritdoc/>
    public async Task<IEnumerable<Documento>> ObterPublicadosPorAnimalAsync(Guid animalId)
    {
        // O documento nao guarda o animal: ele pertence a consulta, e a consulta ao
        // animal. Resolver por ids evita um join que o InMemory dos testes nao cobre
        // do mesmo jeito que o Oracle.
        var consultas = await _context.Consultas
            .Where(c => c.AnimalId == animalId)
            .Select(c => c.Id)
            .ToListAsync();

        return await _dbSet
            .AsNoTracking()
            .Where(d => d.ConsultaId != null
                        && consultas.Contains(d.ConsultaId.Value)
                        && d.PublicadoEm != null)
            .OrderByDescending(d => d.PublicadoEm)
            .ToListAsync();
    }
}
