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
}
