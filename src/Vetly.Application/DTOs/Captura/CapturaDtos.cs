using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Captura;

/// <summary>
/// Sessão aberta pelo veterinário ao iniciar a consulta (RN-008).
/// </summary>
public class SessaoIniciadaDto
{
    public Guid SessaoCapturaId { get; set; }

    /// <summary>
    /// Falso no plano Básico: a consulta inicia, mas sem IA e sem captura (RN-085).
    /// O prontuário é preenchido manualmente.
    /// </summary>
    public bool CapturaAtiva { get; set; }

    public DateTime IniciadaEm { get; set; }

    /// <summary>Parâmetros que o app deve usar na gravação.</summary>
    public ParametrosDeGravacaoDto? Gravacao { get; set; }

    /// <summary>
    /// Avisos que o veterinário precisa ver antes de começar. Ex.: <c>PesoAusente</c>,
    /// que impede sugestão de dose (RN-081).
    /// </summary>
    public List<string> Avisos { get; set; } = [];
}

/// <summary>Como o app deve gravar o áudio da consulta.</summary>
public class ParametrosDeGravacaoDto
{
    public string Formato { get; set; } = "audio/webm;codecs=opus";
    public int SegundosPorSegmento { get; set; } = 30;
    public int SampleRate { get; set; } = 16000;
}

/// <summary>Um trecho de áudio enviado durante a captura (RN-009).</summary>
public class EnviarSegmentoDto
{
    /// <summary>Ordem do trecho na consulta.</summary>
    [Range(0, int.MaxValue, ErrorMessage = "A sequência não pode ser negativa.")]
    public int Sequencia { get; set; }

    /// <summary>Mídia com o áudio, já enviada ao storage.</summary>
    [Required(ErrorMessage = "A mídia do áudio é obrigatória.")]
    public Guid MidiaId { get; set; }

    /// <summary>Duração do trecho, em milissegundos.</summary>
    [Range(1, 600000, ErrorMessage = "A duração deve estar entre 1 ms e 10 minutos.")]
    public int DuracaoMs { get; set; }

    /// <summary>Início do trecho em relação ao começo da consulta.</summary>
    [Range(0, int.MaxValue)]
    public int InicioRelativoMs { get; set; }
}

/// <summary>Confirmação do recebimento de um trecho.</summary>
public class SegmentoRecebidoDto
{
    public Guid SegmentoId { get; set; }
    public int Sequencia { get; set; }
    public EstadoSegmentoAudio Estado { get; set; }
}

/// <summary>Situação da captura de uma consulta (RN-009).</summary>
public class EstadoDaCapturaDto
{
    public Guid SessaoCapturaId { get; set; }
    public EstadoSessaoCaptura Estado { get; set; }
    public bool CapturaAtiva { get; set; }
    public DateTime IniciadaEm { get; set; }
    public DateTime? EncerradaEm { get; set; }

    public int SegmentosRecebidos { get; set; }
    public int SegmentosTranscritos { get; set; }
    public int SegmentosComFalha { get; set; }

    /// <summary>Texto parcial, na ordem dos segmentos já transcritos.</summary>
    public string TextoParcial { get; set; } = string.Empty;

    public List<SegmentoDaCapturaDto> Segmentos { get; set; } = [];
}

/// <summary>Um trecho na visão de acompanhamento da captura.</summary>
public class SegmentoDaCapturaDto
{
    public Guid Id { get; set; }
    public int Sequencia { get; set; }
    public EstadoSegmentoAudio Estado { get; set; }
    public MotivoFalhaTranscricao? FalhaMotivo { get; set; }
    public int Tentativas { get; set; }
}

/// <summary>Resultado do encerramento da consulta (RN-008).</summary>
public class ConsultaEncerradaDto
{
    public Guid ConsultaId { get; set; }

    /// <summary>Estado da consulta — passa a <c>Realizada</c> (RN-038).</summary>
    public StatusConsulta StatusConsulta { get; set; }

    /// <summary>Estado do ciclo de documentação (§7.3).</summary>
    public EstadoSessaoCaptura EstadoDaSessao { get; set; }

    public DateTime EncerradaEm { get; set; }

    /// <summary>Trechos ainda sem desfecho quando a consulta foi encerrada.</summary>
    public int SegmentosPendentes { get; set; }
}

/// <summary>
/// Texto devolvido pelo motor de transcrição (§5.3). Contrato da Vetly, não do motor.
/// </summary>
public class CallbackDeTranscricaoDto
{
    [Required(ErrorMessage = "O segmento é obrigatório.")]
    public Guid SegmentoId { get; set; }

    public Guid ConsultaId { get; set; }

    /// <summary><c>Ok</c> ou <c>Falha</c>.</summary>
    [Required(ErrorMessage = "O status é obrigatório.")]
    public string Status { get; set; } = string.Empty;

    public string? Texto { get; set; }

    [Range(0, 1)]
    public decimal? Confianca { get; set; }

    public string? Idioma { get; set; }

    /// <summary>Motivo, quando o status é falha.</summary>
    public MotivoFalhaTranscricao? Motivo { get; set; }

    /// <summary>Trechos com marcação de tempo, em JSON.</summary>
    public string? Trechos { get; set; }

    /// <summary>Motor e versão que produziram o texto.</summary>
    public MotorDeTranscricaoDto? Motor { get; set; }
}

/// <summary>Identificação do motor que transcreveu.</summary>
public class MotorDeTranscricaoDto
{
    public string Nome { get; set; } = string.Empty;
    public string Versao { get; set; } = string.Empty;
}
