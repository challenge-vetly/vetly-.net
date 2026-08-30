using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório da lista de espera.</summary>
public class ListaEsperaRepository : IListaEsperaRepository
{
    private readonly VetlyDbContext _context;

    public ListaEsperaRepository(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<ItemListaEspera?> ObterPorIdAsync(Guid id) =>
        await _context.ListaDeEspera.FirstOrDefaultAsync(i => i.Id == id);

    /// <inheritdoc/>
    public async Task<IEnumerable<ItemListaEspera>> ObterDoTutorAsync(Guid tutorId) =>
        await _context.ListaDeEspera
            .Where(i => i.TutorId == tutorId)
            .OrderByDescending(i => i.CriadoEm)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<ItemListaEspera?> ObterAguardandoDoAnimalAsync(Guid animalId, Guid veterinarioId) =>
        await _context.ListaDeEspera.FirstOrDefaultAsync(i =>
            i.AnimalId == animalId &&
            i.VeterinarioId == veterinarioId &&
            (i.Estado == EstadoListaEspera.Aguardando || i.Estado == EstadoListaEspera.Notificado));

    /// <inheritdoc/>
    public async Task<ItemListaEspera?> ObterPrimeiroAguardandoAsync(Guid veterinarioId) =>
        await _context.ListaDeEspera
            .Where(i => i.VeterinarioId == veterinarioId && i.Estado == EstadoListaEspera.Aguardando)
            .OrderBy(i => i.CriadoEm)
            .FirstOrDefaultAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<ItemListaEspera>> ObterNotificadosVencidosAsync(Guid veterinarioId, DateTime agora) =>
        await _context.ListaDeEspera
            .Where(i => i.VeterinarioId == veterinarioId
                        && i.Estado == EstadoListaEspera.Notificado
                        && i.PrioridadeAte != null && i.PrioridadeAte < agora)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task AdicionarAsync(ItemListaEspera item) => await _context.ListaDeEspera.AddAsync(item);

    /// <inheritdoc/>
    public void Atualizar(ItemListaEspera item) => _context.ListaDeEspera.Update(item);

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() => await _context.SaveChangesAsync();
}
