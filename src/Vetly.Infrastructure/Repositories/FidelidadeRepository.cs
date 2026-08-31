using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Implementação do extrato de pontos e dos cupons (RN-047 a RN-054).
///
/// O extrato é append-only, com uma exceção deliberada: o campo <c>Restante</c> do
/// lote muda no consumo FIFO. O valor do lançamento nunca muda.
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
    public async Task<IEnumerable<MovimentoDePontos>> ObterLotesComSaldoAsync(Guid tutorId) =>
        // Rastreado de propósito: o consumo FIFO altera o Restante destes lotes
        await _context.MovimentosDePontos
            .Where(m => m.TutorId == tutorId
                        && m.Tipo == TipoMovimentoDePontos.Credito
                        && m.Restante > 0)
            .OrderBy(m => m.ExpiraEm)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<MovimentoDePontos?> ObterCreditoDaConsultaAsync(Guid consultaId) =>
        await _context.MovimentosDePontos
            .FirstOrDefaultAsync(m => m.ConsultaId == consultaId
                                      && m.Tipo == TipoMovimentoDePontos.Credito);

    /// <inheritdoc/>
    public async Task<MovimentoDePontos?> ObterEstornoDaConsultaAsync(Guid consultaId) =>
        await _context.MovimentosDePontos
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ConsultaId == consultaId
                                      && m.Tipo == TipoMovimentoDePontos.Estorno);

    /// <inheritdoc/>
    public async Task<MovimentoDePontos?> ObterCreditoDaObrigacaoAsync(Guid obrigacaoId) =>
        await _context.MovimentosDePontos
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ObrigacaoId == obrigacaoId
                                      && m.Tipo == TipoMovimentoDePontos.Credito);

    /// <inheritdoc/>
    public async Task<IEnumerable<MovimentoDePontos>> ObterCreditosVencidosSemBaixaAsync(DateTime agora) =>
        // O saldo do lote é o que marca o que falta processar: lote zerado já foi
        // gasto ou já expirou, e não precisa de nova baixa.
        await _context.MovimentosDePontos
            .Where(m => m.Tipo == TipoMovimentoDePontos.Credito
                        && m.Restante > 0
                        && m.ExpiraEm != null
                        && m.ExpiraEm <= agora)
            .OrderBy(m => m.ExpiraEm)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task AdicionarAsync(MovimentoDePontos movimento) =>
        await _context.MovimentosDePontos.AddAsync(movimento);

    /// <inheritdoc/>
    public void Atualizar(MovimentoDePontos movimento) => _context.MovimentosDePontos.Update(movimento);

    /// <inheritdoc/>
    public async Task<CupomResgate?> ObterCupomAsync(Guid cupomId) =>
        await _context.CuponsDeResgate.FirstOrDefaultAsync(c => c.Id == cupomId);

    /// <inheritdoc/>
    public async Task<IEnumerable<CupomResgate>> ObterCuponsDoTutorAsync(Guid tutorId) =>
        await _context.CuponsDeResgate
            .AsNoTracking()
            .Where(c => c.TutorId == tutorId)
            .OrderByDescending(c => c.EmitidoEm)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<CupomResgate>> ObterCuponsVencidosAsync(DateTime agora) =>
        await _context.CuponsDeResgate
            .Where(c => c.Status == StatusCupom.Emitido && c.ExpiraEm <= agora)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task AdicionarCupomAsync(CupomResgate cupom) =>
        await _context.CuponsDeResgate.AddAsync(cupom);

    /// <inheritdoc/>
    public void AtualizarCupom(CupomResgate cupom) => _context.CuponsDeResgate.Update(cupom);

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() => await _context.SaveChangesAsync();
}
