using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="ConsentimentoLgpd"/>.</summary>
public class ConsentimentoLgpdRepository : RepositoryBase<ConsentimentoLgpd>, IConsentimentoLgpdRepository
{
    public ConsentimentoLgpdRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<ConsentimentoLgpd>> ObterPorResponsavelAsync(Guid responsavelId) =>
        await _dbSet
            .Where(c => c.ResponsavelId == responsavelId)
            .OrderByDescending(c => c.DataConcessao)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<ConsentimentoLgpd?> ObterAtivoAsync(Guid responsavelId, FinalidadeConsentimento finalidade) =>
        await _dbSet
            .Where(c => c.ResponsavelId == responsavelId
                && c.Finalidade == finalidade
                && c.DataRevogacao == null)
            .OrderByDescending(c => c.DataConcessao)
            .FirstOrDefaultAsync();
}
