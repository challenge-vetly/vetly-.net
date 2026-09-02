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

    /// <summary>
    /// Segmentos sem desfecho que esperam desde antes de <paramref name="limite"/> — a
    /// varredura de trecho travado (§4.2).
    /// </summary>
    /// <remarks>
    /// Cobre os dois jeitos de travar: <c>Enviado</c> cujo callback nunca voltou, e
    /// <c>Recebido</c> que ficou sem job vivo para despachá-lo. Os dois prendem a
    /// sessão em <c>AguardandoTranscricao</c> do mesmo jeito.
    /// </remarks>
    Task<IEnumerable<SegmentoAudio>> ObterSegmentosComEsperaEsgotadaAsync(DateTime limite);

    Task AdicionarSegmentoAsync(SegmentoAudio segmento);
    void AtualizarSegmento(SegmentoAudio segmento);

    /// <summary>Transcrições de uma sessão, para montar o texto na ordem.</summary>
    Task<IEnumerable<Transcricao>> ObterTranscricoesAsync(Guid sessaoId);

    Task AdicionarTranscricaoAsync(Transcricao transcricao);

    /// <summary>Rascunho de uma sessão. Há no máximo um por sessão.</summary>
    Task<RascunhoIa?> ObterRascunhoDaSessaoAsync(Guid sessaoId);

    /// <summary>Rascunho de uma consulta, para a tela de revisão do veterinário.</summary>
    Task<RascunhoIa?> ObterRascunhoDaConsultaAsync(Guid consultaId);

    Task AdicionarRascunhoAsync(RascunhoIa rascunho);

    Task<int> SalvarAsync();
}
