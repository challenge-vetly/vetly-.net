using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de avaliações (RN-055/RN-057).</summary>
public class AvaliacaoRepository : IAvaliacaoRepository
{
    private readonly VetlyDbContext _context;

    public AvaliacaoRepository(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<Avaliacao?> ObterPorIdAsync(Guid id) =>
        await _context.Avaliacoes.FirstOrDefaultAsync(a => a.Id == id);

    /// <inheritdoc/>
    public async Task<Avaliacao?> ObterDaConsultaAsync(Guid consultaId) =>
        await _context.Avaliacoes.AsNoTracking().FirstOrDefaultAsync(a => a.ConsultaId == consultaId);

    /// <inheritdoc/>
    public async Task<IEnumerable<Avaliacao>> ObterDoVeterinarioAsync(Guid veterinarioId) =>
        await _context.Avaliacoes
            .AsNoTracking()
            .Where(a => a.VeterinarioId == veterinarioId)
            .OrderByDescending(a => a.CriadaEm)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task AdicionarAsync(Avaliacao avaliacao) => await _context.Avaliacoes.AddAsync(avaliacao);

    /// <inheritdoc/>
    public void Atualizar(Avaliacao avaliacao) => _context.Avaliacoes.Update(avaliacao);

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() => await _context.SaveChangesAsync();
}
