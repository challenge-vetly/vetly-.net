using Microsoft.EntityFrameworkCore;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório da agenda (configuração, horários e serviços).
/// </summary>
public class AgendaRepository : IAgendaRepository
{
    private readonly VetlyDbContext _context;

    public AgendaRepository(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<AgendaConfig?> ObterConfigAsync(Guid veterinarioId) =>
        await _context.AgendaConfigs.FirstOrDefaultAsync(a => a.VeterinarioId == veterinarioId);

    /// <inheritdoc/>
    public async Task AdicionarConfigAsync(AgendaConfig config) =>
        await _context.AgendaConfigs.AddAsync(config);

    /// <inheritdoc/>
    public void AtualizarConfig(AgendaConfig config) => _context.AgendaConfigs.Update(config);

    /// <inheritdoc/>
    public async Task<IEnumerable<Slot>> ObterSlotsAsync(Guid veterinarioId, DateTime de, DateTime ate) =>
        await _context.Slots
            .Where(s => s.VeterinarioId == veterinarioId && s.Inicio >= de && s.Inicio < ate)
            .OrderBy(s => s.Inicio)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<HashSet<DateTime>> ObterIniciosMaterializadosAsync(Guid veterinarioId, DateTime de) =>
        [.. await _context.Slots
            .Where(s => s.VeterinarioId == veterinarioId && s.Inicio >= de)
            .Select(s => s.Inicio)
            .ToListAsync()];

    /// <inheritdoc/>
    public async Task<Slot?> ObterSlotAsync(Guid slotId) =>
        await _context.Slots.FirstOrDefaultAsync(s => s.Id == slotId);

    /// <inheritdoc/>
    public async Task AdicionarSlotsAsync(IEnumerable<Slot> slots) =>
        await _context.Slots.AddRangeAsync(slots);

    /// <inheritdoc/>
    public void AtualizarSlot(Slot slot) => _context.Slots.Update(slot);

    /// <inheritdoc/>
    public async Task<Dictionary<Guid, int>> ContarDisponiveisNasProximas48hAsync(
        IEnumerable<Guid> veterinarioIds, DateTime agora)
    {
        var ids = veterinarioIds.ToList();
        var limite = agora.AddHours(48);

        var contagem = await _context.Slots
            .Where(s => ids.Contains(s.VeterinarioId)
                        && s.Inicio >= agora && s.Inicio < limite
                        && s.Estado == EstadoSlot.Livre)
            .GroupBy(s => s.VeterinarioId)
            .Select(g => new { VeterinarioId = g.Key, Total = g.Count() })
            .ToListAsync();

        return contagem.ToDictionary(c => c.VeterinarioId, c => c.Total);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<Guid, DateTime>> ObterProximoHorarioLivreAsync(
        IEnumerable<Guid> veterinarioIds, DateTime agora)
    {
        var ids = veterinarioIds.ToList();

        var proximos = await _context.Slots
            .Where(s => ids.Contains(s.VeterinarioId) && s.Inicio > agora && s.Estado == EstadoSlot.Livre)
            .GroupBy(s => s.VeterinarioId)
            .Select(g => new { VeterinarioId = g.Key, Inicio = g.Min(s => s.Inicio) })
            .ToListAsync();

        return proximos.ToDictionary(p => p.VeterinarioId, p => p.Inicio);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Servico>> ObterServicosAsync(Guid prestadorId) =>
        await _context.Servicos
            .Where(s => s.PrestadorId == prestadorId && s.Ativo)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<Servico?> ObterServicoAsync(Guid servicoId) =>
        await _context.Servicos.FirstOrDefaultAsync(s => s.Id == servicoId);

    /// <inheritdoc/>
    public async Task AdicionarServicoAsync(Servico servico) => await _context.Servicos.AddAsync(servico);

    /// <inheritdoc/>
    public void AtualizarServico(Servico servico) => _context.Servicos.Update(servico);

    /// <inheritdoc/>
    ///
    /// <remarks>
    /// Traduz a colisao de concorrencia do slot em conflito de estado (RN-035).
    ///
    /// O <c>DbUpdateConcurrencyException</c> e detalhe do EF Core e nao deve subir para
    /// a Application — mas o significado dele aqui e exato e precisa chegar ao cliente:
    /// outra pessoa ficou com o horario entre a leitura e a gravacao. A traducao vive
    /// nesta fronteira justamente para que a camada de cima continue sem conhecer o
    /// ORM, e para que qualquer chamador que grave slot ganhe o 409 de graca.
    ///
    /// A mensagem e a mesma da guarda otimista do servico de proposito: para quem
    /// esta do outro lado, perder o horario para outra pessoa e perder o horario para
    /// outra pessoa — nao interessa se a corrida foi decidida em memoria ou no banco.
    /// </remarks>
    public async Task<int> SalvarAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex) when (ex.Entries.Any(e => e.Entity is Slot))
        {
            // O contexto ficou com o slot em estado sujo: sem descartar o rastreamento,
            // a proxima gravacao desta requisicao repetiria a mesma falha.
            foreach (var entrada in ex.Entries)
                entrada.State = EntityState.Detached;

            throw new ConflitoDeEstadoException("RN-035",
                "Este horario acabou de ser reservado por outra pessoa.");
        }
    }
}
