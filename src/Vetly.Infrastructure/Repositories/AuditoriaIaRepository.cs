using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Implementação da trilha de auditoria da IA (RN-082).
///
/// Só adiciona e lê. Não existe atualizar nem remover aqui de propósito: um registro
/// que pode ser reescrito depois não prova que houve decisão humana.
/// </summary>
public class AuditoriaIaRepository : IAuditoriaIaRepository
{
    private readonly VetlyDbContext _context;

    public AuditoriaIaRepository(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task AdicionarAsync(LogAuditoriaIa registro) =>
        await _context.LogsDeAuditoriaIa.AddAsync(registro);

    /// <inheritdoc/>
    public async Task<IEnumerable<LogAuditoriaIa>> ObterDaConsultaAsync(Guid consultaId) =>
        await _context.LogsDeAuditoriaIa
            .AsNoTracking()
            .Where(l => l.ConsultaId == consultaId)
            .OrderByDescending(l => l.RegistradoEm)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() => await _context.SaveChangesAsync();
}
