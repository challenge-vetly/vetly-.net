namespace Vetly.Domain.Enums;

/// <summary>Situação de um segmento de áudio da consulta (RN-009).</summary>
public enum EstadoSegmentoAudio
{
    /// <summary>Áudio registrado, aguardando o despacho para transcrição.</summary>
    Recebido = 1,

    /// <summary>Despachado ao motor de transcrição, aguardando o callback.</summary>
    Enviado = 2,

    /// <summary>Texto recebido e persistido.</summary>
    Transcrito = 3,

    /// <summary>Falhou em todas as tentativas.</summary>
    Falha = 4
}

/// <summary>
/// Por que um segmento não foi transcrito. Serve ao aviso que o veterinário vê no
/// rascunho e ao diagnóstico de operação.
/// </summary>
public enum MotivoFalhaTranscricao
{
    /// <summary>Áudio inaudível ou corrompido.</summary>
    AudioIlegivel = 1,

    /// <summary>Formato de áudio não suportado pelo motor.</summary>
    FormatoNaoSuportado = 2,

    /// <summary>O motor de transcrição não respondeu.</summary>
    MotorIndisponivel = 3,

    /// <summary>O callback não chegou dentro da janela esperada.</summary>
    Timeout = 4
}
