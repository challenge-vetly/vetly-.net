namespace Vetly.Domain.Enums;

/// <summary>
/// Estado da consulta no ciclo de vida do agendamento (RN-035, RN-038, RN-040, RN-044).
///
/// Substitui a representação anterior, espalhada em três booleanos
/// (<c>Cancelada</c>, <c>Finalizada</c>, <c>StatusPagamento</c>), que não expressava
/// a máquina de estados e não distinguia, por exemplo, no-show de cancelamento.
/// Os booleanos seguem sendo escritos por uma release (dupla escrita) até que os
/// consumidores migrem.
/// </summary>
public enum StatusConsulta
{
    /// <summary>
    /// Slot travado e consulta criada, aguardando confirmação do pagamento.
    /// O lock expira em 10 minutos (RN-035).
    /// </summary>
    EmCheckout = 1,

    /// <summary>Pagamento confirmado; a consulta está agendada (RN-006).</summary>
    Confirmada = 2,

    /// <summary>Atendimento realizado — dispara avaliação (RN-055) e pontuação (RN-052).</summary>
    Realizada = 3,

    /// <summary>Cancelada pelo Responsável, pelo prestador ou por offboarding (RN-041/RN-045).</summary>
    Cancelada = 4,

    /// <summary>Responsável não compareceu. Sem reembolso, seguindo a faixa "&lt; 2h" (RN-044).</summary>
    NoShow = 5,

    /// <summary>Lock de checkout expirou sem confirmação do pagamento (RN-035).</summary>
    Expirada = 6
}
