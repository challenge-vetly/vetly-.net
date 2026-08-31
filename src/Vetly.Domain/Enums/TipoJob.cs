namespace Vetly.Domain.Enums;

/// <summary>
/// Trabalhos de negócio executados fora do ciclo da requisição (§11).
/// </summary>
public enum TipoJob
{
    /// <summary>
    /// Confirmação simulada de pagamento, agendada logo após a criação da cobrança.
    /// É o que faz o webhook do provedor simulado chegar sozinho (§5.1).
    /// </summary>
    ConfirmarPagamentoSimulado = 1,

    /// <summary>
    /// Oferece um horário liberado ao primeiro da lista de espera (RN-037).
    /// Enfileirado sempre que um horário volta a ficar livre.
    /// </summary>
    PromoverListaEspera = 2,

    /// <summary>
    /// Despacha um segmento de áudio ao motor de transcrição (RN-009).
    /// Retentado com espera crescente quando o motor não aceita o trabalho.
    /// </summary>
    TranscreverSegmento = 3,

    /// <summary>
    /// Entrega a transcrição do motor simulado pelo mesmo callback que o fluxo real
    /// usaria — assim o caminho assíncrono é exercitado de verdade (§5.3).
    /// </summary>
    TranscreverSegmentoSimulado = 4,

    /// <summary>
    /// Estrutura a transcrição da consulta em prontuário pela IA (RN-080).
    /// Enfileirado quando todos os trechos tiveram desfecho e a janela já fechou.
    /// </summary>
    EstruturarConsulta = 5,

    /// <summary>
    /// Credita os pontos de fidelidade de uma consulta realizada (RN-052).
    /// Enfileirado quando o veterinário encerra o atendimento.
    /// </summary>
    CreditarPontosDaConsulta = 6
}
