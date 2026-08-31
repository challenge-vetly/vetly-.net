using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de <see cref="Animal"/> com suporte ao histórico longitudinal.
/// </summary>
public class AnimalRepository : RepositoryBase<Animal>, IAnimalRepository
{
    public AnimalRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<Animal>> ObterPorTutorAsync(Guid tutorId) =>
        await _dbSet
            .Where(a => a.TutorId == tutorId && a.Ativo)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Prontuario>> ObterHistoricoLongitudinalAsync(Guid animalId) =>
        await _context.Prontuarios
            .Where(p => p.AnimalId == animalId)
            .OrderByDescending(p => p.DataCriacao) // mais recente primeiro
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Exame>> ObterExamesAsync(Guid animalId) =>
        await _context.Exames
            .Where(e => e.AnimalId == animalId)
            .OrderByDescending(e => e.DataSolicitacao)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Animal>> ObterAtivosAsync() =>
        await _dbSet
            .Where(a => a.Ativo)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Animal>> ObterPorVeterinarioAsync(Guid veterinarioId) =>
        await _dbSet
            .Where(a => a.Ativo && _context.Consultas
                .Any(c => c.VeterinarioId == veterinarioId && c.AnimalId == a.Id))
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Consulta>> ObterConsultasFuturasAsync(Guid animalId, DateTime agora) =>
        // O board mostra o que vai acontecer: cancelada e expirada ficam de fora
        await _context.Consultas
            .AsNoTracking()
            .Where(c => c.AnimalId == animalId
                        && c.DataHora >= agora
                        && c.Status != StatusConsulta.Cancelada
                        && c.Status != StatusConsulta.Expirada)
            .OrderBy(c => c.DataHora)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<bool> VeterinarioAtendeAnimalAsync(Guid veterinarioId, Guid animalId) =>
        await _context.Consultas
            .AnyAsync(c => c.VeterinarioId == veterinarioId && c.AnimalId == animalId);
}
