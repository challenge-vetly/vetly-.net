using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório da colmeia (RN-090).
///
/// O log só é adicionado e lido. Não existe atualizar nem remover aqui de propósito:
/// registro de acesso que pode ser apagado não serve para auditar acesso.
/// </summary>
public class ColmeiaRepository : IColmeiaRepository
{
    private readonly VetlyDbContext _context;

    public ColmeiaRepository(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<AcessoColmeia?> ObterPorIdAsync(Guid id) =>
        await _context.AcessosDaColmeia.FirstOrDefaultAsync(a => a.Id == id);

    /// <inheritdoc/>
    public async Task<AcessoColmeia?> ObterVigenteAsync(Guid animalId, Guid veterinarioId, DateTime agora) =>
        await _context.AcessosDaColmeia
            .Where(a => a.AnimalId == animalId
                        && a.VeterinarioId == veterinarioId
                        && a.RevogadoEm == null
                        && a.ExpiraEm > agora)
            .OrderByDescending(a => a.ConcedidoEm)
            .FirstOrDefaultAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<AcessoColmeia>> ObterDoAnimalAsync(Guid animalId) =>
        await _context.AcessosDaColmeia
            .AsNoTracking()
            .Where(a => a.AnimalId == animalId)
            .OrderByDescending(a => a.ConcedidoEm)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task AdicionarAsync(AcessoColmeia acesso) =>
        await _context.AcessosDaColmeia.AddAsync(acesso);

    /// <inheritdoc/>
    public void Atualizar(AcessoColmeia acesso) => _context.AcessosDaColmeia.Update(acesso);

    /// <inheritdoc/>
    public async Task<IEnumerable<LogAcessoColmeia>> ObterLogDoAnimalAsync(Guid animalId) =>
        await _context.LogsDeAcessoDaColmeia
            .AsNoTracking()
            .Where(l => l.AnimalId == animalId)
            .OrderByDescending(l => l.OcorridoEm)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task AdicionarLogAsync(LogAcessoColmeia registro) =>
        await _context.LogsDeAcessoDaColmeia.AddAsync(registro);

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() => await _context.SaveChangesAsync();
}
