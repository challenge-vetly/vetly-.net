using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Implementação do extrato de pontos (RN-051/RN-052).
///
/// Só adiciona e lê: um extrato que pode ser reescrito não sustenta o saldo que
/// mostra.
/// </summary>
public class FidelidadeRepository : IFidelidadeRepository
{
    private readonly VetlyDbContext _context;

    public FidelidadeRepository(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<IEnumerable<MovimentoDePontos>> ObterDoTutorAsync(Guid tutorId) =>
        await _context.MovimentosDePontos
            .AsNoTracking()
            .Where(m => m.TutorId == tutorId)
            .OrderByDescending(m => m.OcorridoEm)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<MovimentoDePontos?> ObterCreditoDaConsultaAsync(Guid consultaId) =>
        await _context.MovimentosDePontos
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ConsultaId == consultaId
                                      && m.Tipo == TipoMovimentoDePontos.Credito);

    /// <inheritdoc/>
    public async Task<IEnumerable<MovimentoDePontos>> ObterCreditosVencidosSemBaixaAsync(DateTime agora)
    {
        // A ausência da baixa é o que marca o que falta processar: a tabela é
        // append-only e não tem coluna de "já tratado".
        var jaBaixados = await _context.MovimentosDePontos
            .Where(m => m.Tipo == TipoMovimentoDePontos.Expiracao && m.MovimentoOrigemId != null)
            .Select(m => m.MovimentoOrigemId)
            .ToListAsync();

        return await _context.MovimentosDePontos
            .AsNoTracking()
            .Where(m => m.Tipo == TipoMovimentoDePontos.Credito
                        && m.ExpiraEm != null
                        && m.ExpiraEm <= agora
                        && !jaBaixados.Contains(m.Id))
            .OrderBy(m => m.ExpiraEm)
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task AdicionarAsync(MovimentoDePontos movimento) =>
        await _context.MovimentosDePontos.AddAsync(movimento);

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() => await _context.SaveChangesAsync();
}
