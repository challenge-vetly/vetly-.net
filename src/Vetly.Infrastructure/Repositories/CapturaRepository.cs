using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Implementação do repositório da captura de áudio.</summary>
public class CapturaRepository : ICapturaRepository
{
    private readonly VetlyDbContext _context;

    public CapturaRepository(VetlyDbContext context) => _context = context;

    /// <inheritdoc/>
    public async Task<SessaoCaptura?> ObterSessaoAsync(Guid sessaoId) =>
        await _context.SessoesDeCaptura.FirstOrDefaultAsync(s => s.Id == sessaoId);

    /// <inheritdoc/>
    public async Task<SessaoCaptura?> ObterSessaoDaConsultaAsync(Guid consultaId) =>
        await _context.SessoesDeCaptura.FirstOrDefaultAsync(s => s.ConsultaId == consultaId);

    /// <inheritdoc/>
    public async Task AdicionarSessaoAsync(SessaoCaptura sessao) =>
        await _context.SessoesDeCaptura.AddAsync(sessao);

    /// <inheritdoc/>
    public void AtualizarSessao(SessaoCaptura sessao) => _context.SessoesDeCaptura.Update(sessao);

    /// <inheritdoc/>
    public async Task<SegmentoAudio?> ObterSegmentoAsync(Guid segmentoId) =>
        await _context.SegmentosDeAudio.FirstOrDefaultAsync(s => s.Id == segmentoId);

    /// <inheritdoc/>
    public async Task<SegmentoAudio?> ObterSegmentoPorSequenciaAsync(Guid sessaoId, int sequencia) =>
        await _context.SegmentosDeAudio
            .FirstOrDefaultAsync(s => s.SessaoCapturaId == sessaoId && s.Sequencia == sequencia);

    /// <inheritdoc/>
    public async Task<IEnumerable<SegmentoAudio>> ObterSegmentosAsync(Guid sessaoId) =>
        await _context.SegmentosDeAudio
            .Where(s => s.SessaoCapturaId == sessaoId)
            .OrderBy(s => s.Sequencia)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task AdicionarSegmentoAsync(SegmentoAudio segmento) =>
        await _context.SegmentosDeAudio.AddAsync(segmento);

    /// <inheritdoc/>
    public void AtualizarSegmento(SegmentoAudio segmento) => _context.SegmentosDeAudio.Update(segmento);

    /// <inheritdoc/>
    public async Task<IEnumerable<Transcricao>> ObterTranscricoesAsync(Guid sessaoId)
    {
        var idsDosSegmentos = await _context.SegmentosDeAudio
            .Where(s => s.SessaoCapturaId == sessaoId)
            .Select(s => s.Id)
            .ToListAsync();

        return await _context.Transcricoes
            .Where(t => idsDosSegmentos.Contains(t.SegmentoAudioId))
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task AdicionarTranscricaoAsync(Transcricao transcricao) =>
        await _context.Transcricoes.AddAsync(transcricao);

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() => await _context.SaveChangesAsync();
}
