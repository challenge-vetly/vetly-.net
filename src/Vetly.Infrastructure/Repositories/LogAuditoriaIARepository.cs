using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="LogAuditoriaIA"/>.</summary>
public class LogAuditoriaIARepository : RepositoryBase<LogAuditoriaIA>, ILogAuditoriaIARepository
{
    public LogAuditoriaIARepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<LogAuditoriaIA>> ObterPorConsultaAsync(Guid consultaId) =>
        await _dbSet
            .Where(l => l.ConsultaId == consultaId)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<LogAuditoriaIA?> ObterPendenteAsync(Guid consultaId, TipoSugestaoIA tipoSugestao) =>
        await _dbSet
            .Where(l => l.ConsultaId == consultaId && l.TipoSugestao == tipoSugestao && l.Decisao == null)
            .OrderByDescending(l => l.Timestamp)
            .FirstOrDefaultAsync();
}
