using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de <see cref="Midia"/>.</summary>
public class MidiaRepository : IMidiaRepository
{
    private readonly VetlyDbContext _context;

    public MidiaRepository(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<Midia?> ObterPorIdAsync(Guid id) =>
        await _context.Midias.FirstOrDefaultAsync(m => m.Id == id);

    /// <inheritdoc/>
    public async Task<Midia?> ObterPorChaveAsync(string chaveStorage) =>
        await _context.Midias.FirstOrDefaultAsync(m => m.ChaveStorage == chaveStorage);

    /// <inheritdoc/>
    public async Task<IEnumerable<Midia>> ObterDaConsultaAsync(Guid consultaId) =>
        await _context.Midias.Where(m => m.ConsultaId == consultaId).ToListAsync();

    /// <inheritdoc/>
    public async Task AdicionarAsync(Midia midia) => await _context.Midias.AddAsync(midia);

    /// <inheritdoc/>
    public void Atualizar(Midia midia) => _context.Midias.Update(midia);

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() => await _context.SaveChangesAsync();
}
