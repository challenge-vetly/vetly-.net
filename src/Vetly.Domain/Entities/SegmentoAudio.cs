using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Domain.Entities;

/// <summary>
/// Um trecho de áudio da consulta, enviado durante a janela de captura (RN-009).
///
/// O áudio é gravado em segmentos curtos em vez de um arquivo único: assim a
/// transcrição vai acontecendo durante o atendimento, e a falha de um trecho não
/// derruba a consulta inteira.
/// </summary>
public class SegmentoAudio
{
    /// <summary>Tentativas de despacho ao motor antes de desistir do segmento.</summary>
    public const int MaximoDeTentativas = 3;

    /// <summary>Identificador do segmento (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Sessão de captura a que pertence.</summary>
    [Required]
    public Guid SessaoCapturaId { get; private set; }

    /// <summary>Ordem do segmento na consulta. É o que reconstrói o texto na ordem certa.</summary>
    public int Sequencia { get; private set; }

    /// <summary>Mídia com o áudio, no storage de objetos.</summary>
    [Required]
    public Guid MidiaId { get; private set; }

    /// <summary>Duração do trecho, em milissegundos.</summary>
    public int DuracaoMs { get; private set; }

    /// <summary>Início do trecho em relação ao começo da consulta, em milissegundos.</summary>
    public int InicioRelativoMs { get; private set; }

    /// <summary>Situação da transcrição deste trecho.</summary>
    [Required]
    public EstadoSegmentoAudio Estado { get; private set; }

    /// <summary>Por que falhou, quando falhou.</summary>
    public MotivoFalhaTranscricao? FalhaMotivo { get; private set; }

    /// <summary>Quantas vezes o despacho foi tentado.</summary>
    public int Tentativas { get; private set; }

    /// <summary>
    /// Hash do token que o motor devolve no callback. Guardar o hash, e não o token,
    /// evita que vazamento da tabela permita forjar uma transcrição.
    /// </summary>
    [MaxLength(64)]
    public string? CallbackTokenHash { get; private set; }

    /// <summary>
    /// Quando o segmento foi entregue ao motor. É o relógio a partir do qual se conta
    /// o tempo de espera pelo callback (§4.2): sem ele não há como distinguir um
    /// segmento que acabou de sair de um que o motor engoliu e nunca respondeu.
    /// </summary>
    public DateTime? DespachadoEm { get; private set; }

    public DateTime CriadoEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private SegmentoAudio() { }

    /// <summary>Registra um trecho de áudio recebido durante a captura.</summary>
    public SegmentoAudio(Guid sessaoCapturaId, int sequencia, Guid midiaId, int duracaoMs, int inicioRelativoMs)
    {
        if (sequencia < 0)
            throw new ArgumentOutOfRangeException(nameof(sequencia), "A sequência não pode ser negativa.");

        if (duracaoMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(duracaoMs), "A duração deve ser maior que zero.");

        Id = Guid.NewGuid();
        SessaoCapturaId = sessaoCapturaId;
        Sequencia = sequencia;
        MidiaId = midiaId;
        DuracaoMs = duracaoMs;
        InicioRelativoMs = inicioRelativoMs;
        Estado = EstadoSegmentoAudio.Recebido;
        CriadoEm = DateTime.UtcNow;
    }

    /// <summary>
    /// Marca o despacho ao motor de transcrição e guarda o hash do token.
    ///
    /// O instante vem de fora, como em <see cref="Job.RegistrarFalha"/>: é dele que a
    /// varredura de trecho travado conta o prazo, e um relógio que a entidade lê
    /// sozinha não é observável de fora.
    /// </summary>
    public void RegistrarDespacho(string callbackTokenHash, DateTime agora)
    {
        Estado = EstadoSegmentoAudio.Enviado;
        CallbackTokenHash = callbackTokenHash;
        DespachadoEm = agora;
        Tentativas++;
    }

    /// <summary>
    /// Verdadeiro quando o segmento saiu para o motor e o callback não voltou dentro
    /// do prazo (§4.2).
    ///
    /// Sem isto o segmento fica em <c>Enviado</c> para sempre quando o motor aceita e
    /// depois morre calado — e a sessão inteira trava em <c>AguardandoTranscricao</c>,
    /// porque o desfecho só é avaliado quando <b>todos</b> os trechos responderam.
    /// </summary>
    public bool AguardaCallbackHaMaisDe(TimeSpan prazo, DateTime agora) =>
        Estado == EstadoSegmentoAudio.Enviado &&
        DespachadoEm is { } despacho && agora - despacho > prazo;

    /// <summary>Registra o texto recebido do motor.</summary>
    public void RegistrarTranscricao() => Estado = EstadoSegmentoAudio.Transcrito;

    /// <summary>
    /// Registra uma falha. Enquanto houver tentativa, volta para a fila; esgotadas,
    /// o segmento é dado como perdido e o rascunho sai sem ele, com aviso.
    /// </summary>
    public void RegistrarFalha(MotivoFalhaTranscricao motivo)
    {
        FalhaMotivo = motivo;

        Estado = Tentativas >= MaximoDeTentativas
            ? EstadoSegmentoAudio.Falha
            : EstadoSegmentoAudio.Recebido;
    }

    /// <summary>Verdadeiro quando o segmento já teve desfecho — transcrito ou perdido.</summary>
    public bool TemDesfecho() => Estado is EstadoSegmentoAudio.Transcrito or EstadoSegmentoAudio.Falha;
}

/// <summary>
/// Texto produzido pelo motor de transcrição para um segmento (RN-009).
///
/// Guardado à parte do segmento porque é conteúdo, não estado: o segmento controla o
/// ciclo, a transcrição carrega o que foi dito.
/// </summary>
public class Transcricao
{
    /// <summary>Identificador da transcrição (chave primária).</summary>
    public Guid Id { get; private set; }

    /// <summary>Segmento transcrito.</summary>
    [Required]
    public Guid SegmentoAudioId { get; private set; }

    /// <summary>Texto reconhecido.</summary>
    [Required]
    public string Texto { get; private set; }

    /// <summary>Confiança relatada pelo motor, de 0 a 1.</summary>
    public decimal? Confianca { get; private set; }

    /// <summary>Trechos com marcação de tempo, em JSON, quando o motor os fornece.</summary>
    public string? Trechos { get; private set; }

    /// <summary>Motor e versão que produziram o texto — parte da trilha de auditoria.</summary>
    [MaxLength(100)]
    public string? Motor { get; private set; }

    public DateTime CriadaEm { get; private set; }

    /// <summary>Construtor privado reservado ao EF Core.</summary>
    private Transcricao() => Texto = null!;

    /// <summary>Registra o texto de um segmento.</summary>
    public Transcricao(Guid segmentoAudioId, string texto, decimal? confianca, string? trechos, string? motor)
    {
        Id = Guid.NewGuid();
        SegmentoAudioId = segmentoAudioId;
        Texto = texto ?? string.Empty;
        Confianca = confianca;
        Trechos = trechos;
        Motor = motor;
        CriadaEm = DateTime.UtcNow;
    }
}
