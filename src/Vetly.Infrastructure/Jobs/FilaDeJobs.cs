using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Jobs;

/// <summary>
/// Fila de trabalhos sobre o Oracle que já existe (§11) — sem broker novo.
/// </summary>
public class FilaDeJobs : IFilaDeJobs
{
    private readonly VetlyDbContext _context;

    public FilaDeJobs(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task EnfileirarAsync(TipoJob tipo, string? payload = null, TimeSpan? atraso = null)
    {
        await _context.Jobs.AddAsync(new Job(tipo, payload, atraso));
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Job>> ObterElegiveisAsync(DateTime agora, int limite) =>
        await _context.Jobs
            .Where(j => j.Estado == EstadoJob.Pendente && j.ExecutarEm <= agora)
            .OrderBy(j => j.ExecutarEm)
            .Take(limite)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() => await _context.SaveChangesAsync();
}
