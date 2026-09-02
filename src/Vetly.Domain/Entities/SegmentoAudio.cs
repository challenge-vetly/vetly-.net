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
    /// Instante a partir do qual se conta a espera pelo desfecho deste trecho (§4.2).
    ///
    /// O último despacho quando houve um; a criação quando não houve. As duas
    /// situações precisam de relógio porque as duas travam a sessão: o motor que
    /// aceita e morre calado deixa o trecho em <c>Enviado</c>, e o job de despacho que
    /// esgota as próprias tentativas deixa o trecho em <c>Recebido</c> — sem job vivo
    /// e sem ninguém para reenfileirá-lo.
    /// </summary>
    public DateTime EsperandoDesde => DespachadoEm ?? CriadoEm;

    /// <summary>
    /// Verdadeiro quando o trecho passou do prazo sem chegar a desfecho (§4.2).
    ///
    /// Vale para <c>Enviado</c> e para <c>Recebido</c>: o que trava a sessão não é o
    /// estado em que o trecho parou, é ele não ter parado em <b>nenhum</b> desfecho —
    /// o desfecho da sessão só é avaliado quando todos os trechos responderam.
    ///
    /// Trecho recém-criado não é trecho travado: enquanto está dentro do prazo, ele
    /// está no fluxo normal, esperando o despacho ou o retorno do motor.
    /// </summary>
    public bool EsperaEsgotada(TimeSpan prazo, DateTime agora) =>
        !TemDesfecho() && agora - EsperandoDesde > prazo;

    /// <summary>
    /// Encerra a espera vencida: conta a tentativa e aplica a falha por timeout (§4.2).
    ///
    /// Em <c>Recebido</c> a tentativa <b>precisa</b> ser contada aqui. O despacho que
    /// nunca vingou não passou por <see cref="RegistrarDespacho"/> e, portanto, não
    /// consumiu tentativa nenhuma — sem contá-la, a varredura reenfileiraria o mesmo
    /// trecho para sempre e trocaria uma sessão travada por um laço de jobs. Em
    /// <c>Enviado</c> a tentativa já foi contada no despacho, e contar de novo comeria
    /// duas de uma vez.
    /// </summary>
    public void EncerrarEsperaVencida()
    {
        if (Estado == EstadoSegmentoAudio.Recebido)
            Tentativas++;

        RegistrarFalha(MotivoFalhaTranscricao.Timeout);
    }

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
