using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório de notificações (RN-092).</summary>
public class NotificacaoRepository : INotificacaoRepository
{
    private readonly VetlyDbContext _context;

    public NotificacaoRepository(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<Notificacao?> ObterPorIdAsync(Guid id) =>
        await _context.Notificacoes.FirstOrDefaultAsync(n => n.Id == id);

    /// <inheritdoc/>
    public async Task<IEnumerable<Notificacao>> ObterDoTutorAsync(Guid tutorId, bool apenasNaoLidas) =>
        await _context.Notificacoes
            .AsNoTracking()
            .Where(n => n.TutorId == tutorId && (!apenasNaoLidas || n.LidaEm == null))
            .OrderByDescending(n => n.CriadaEm)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Notificacao>> ObterPendentesAsync(DateTime agora, int limite) =>
        await _context.Notificacoes
            .AsNoTracking()
            .Where(n => n.Status == StatusNotificacao.Pendente && n.AgendadaPara <= agora)
            .OrderBy(n => n.AgendadaPara)
            .Take(limite)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<Notificacao?> ObterDoAnimalPorTipoDesdeAsync(
        Guid animalId, TipoNotificacao tipo, DateTime desde) =>
        await _context.Notificacoes
            .AsNoTracking()
            .Where(n => n.AnimalId == animalId && n.Tipo == tipo && n.CriadaEm >= desde)
            .OrderByDescending(n => n.CriadaEm)
            .FirstOrDefaultAsync();

    /// <inheritdoc/>
    public async Task AdicionarAsync(Notificacao notificacao) =>
        await _context.Notificacoes.AddAsync(notificacao);

    /// <inheritdoc/>
    public void Atualizar(Notificacao notificacao) => _context.Notificacoes.Update(notificacao);

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() => await _context.SaveChangesAsync();
}
