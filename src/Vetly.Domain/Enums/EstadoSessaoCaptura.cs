namespace Vetly.Domain.Enums;

/// <summary>
/// Estado da janela de captura e do ciclo de documentação da consulta (§7.3).
///
/// A janela é delimitada por "iniciar consulta" e "encerrar consulta" (RN-008/RN-079):
/// fora dela a IA não captura áudio nem produz conteúdo clínico.
/// </summary>
public enum EstadoSessaoCaptura
{
    /// <summary>Janela aberta, recebendo segmentos de áudio (RN-009).</summary>
    Capturando = 1,

    /// <summary>Janela fechada, esperando a transcrição dos segmentos enviados.</summary>
    AguardandoTranscricao = 2,

    /// <summary>
    /// Parte dos segmentos falhou. O rascunho é gerado com o texto disponível e o
    /// veterinário resolve o resto pela correção (RN-082).
    /// </summary>
    TranscricaoParcial = 3,

    /// <summary>
    /// Nenhum segmento transcreveu — ou o plano é Básico, que não tem captura
    /// (RN-085). O caminho é o prontuário manual.
    /// </summary>
    SemTranscricao = 4,

    /// <summary>Transcrição pronta, aguardando a estruturação pela IA (RN-080).</summary>
    GerandoRascunho = 5,

    /// <summary>Rascunho disponível para a decisão do veterinário (RN-082).</summary>
    RascunhoPronto = 6,

    /// <summary>Decisão tomada; documentos sendo gerados a partir do estado final (RN-083).</summary>
    Documentando = 7,

    /// <summary>O veterinário não aprovou: o ciclo encerra sem emitir documentos (RN-082).</summary>
    EncerradaSemDocumentos = 8,

    /// <summary>Documentos gerados, assinados e publicados.</summary>
    Concluida = 9
}
