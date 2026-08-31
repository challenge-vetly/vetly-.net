using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato de repositório da captura de áudio (RN-008/RN-009).
/// </summary>
public interface ICapturaRepository
{
    Task<SessaoCaptura?> ObterSessaoAsync(Guid sessaoId);

    /// <summary>Sessão de uma consulta. Cada consulta tem no máximo uma.</summary>
    Task<SessaoCaptura?> ObterSessaoDaConsultaAsync(Guid consultaId);

    Task AdicionarSessaoAsync(SessaoCaptura sessao);
    void AtualizarSessao(SessaoCaptura sessao);

    Task<SegmentoAudio?> ObterSegmentoAsync(Guid segmentoId);

    /// <summary>Segmento por posição na consulta — é como se detecta reenvio.</summary>
    Task<SegmentoAudio?> ObterSegmentoPorSequenciaAsync(Guid sessaoId, int sequencia);

    Task<IEnumerable<SegmentoAudio>> ObterSegmentosAsync(Guid sessaoId);

    Task AdicionarSegmentoAsync(SegmentoAudio segmento);
    void AtualizarSegmento(SegmentoAudio segmento);

    /// <summary>Transcrições de uma sessão, para montar o texto na ordem.</summary>
    Task<IEnumerable<Transcricao>> ObterTranscricoesAsync(Guid sessaoId);

    Task AdicionarTranscricaoAsync(Transcricao transcricao);

    Task<int> SalvarAsync();
}
