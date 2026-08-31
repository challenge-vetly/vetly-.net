using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório das obrigações de cuidado (RN-045).</summary>
public class ObrigacaoRepository : IObrigacaoRepository
{
    private readonly VetlyDbContext _context;

    public ObrigacaoRepository(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<ObrigacaoPet?> ObterPorIdAsync(Guid id) =>
        await _context.ObrigacoesDoPet.FirstOrDefaultAsync(o => o.Id == id);

    /// <inheritdoc/>
    public async Task<IEnumerable<ObrigacaoPet>> ObterDoAnimalAsync(Guid animalId, bool incluirArquivadas = false) =>
        await _context.ObrigacoesDoPet
            .Where(o => o.AnimalId == animalId && (incluirArquivadas || !o.Arquivada))
            .OrderBy(o => o.ProximoVencimento)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<ObrigacaoPet>> ObterVencendoAteAsync(DateTime limite) =>
        await _context.ObrigacoesDoPet
            .Where(o => !o.Arquivada && o.ProximoVencimento <= limite)
            .OrderBy(o => o.ProximoVencimento)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task AdicionarAsync(ObrigacaoPet obrigacao) =>
        await _context.ObrigacoesDoPet.AddAsync(obrigacao);

    /// <inheritdoc/>
    public void Atualizar(ObrigacaoPet obrigacao) => _context.ObrigacoesDoPet.Update(obrigacao);

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() => await _context.SaveChangesAsync();
}
