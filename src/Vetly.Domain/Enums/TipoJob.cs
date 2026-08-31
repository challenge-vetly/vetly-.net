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
    PromoverListaEspera = 2
}
