namespace Vetly.Domain.Enums;

/// <summary>
/// Natureza de um lançamento no extrato de pontos (RN-051/RN-052).
///
/// Os quatro tipos existem para que o extrato conte o que aconteceu. Saldo que cai
/// sem lançamento correspondente é saldo que o Responsável não consegue conferir.
/// </summary>
public enum TipoMovimentoDePontos
{
    /// <summary>Pontos ganhos por consulta realizada e paga (RN-052).</summary>
    Credito = 1,

    /// <summary>Pontos usados como desconto (RN-051).</summary>
    Debito = 2,

    /// <summary>Pontos que perderam a validade.</summary>
    Expiracao = 3,

    /// <summary>Correção lançada pela operação.</summary>
    Ajuste = 4,

    /// <summary>
    /// Pontos desfeitos por cancelamento ou reembolso da consulta que os gerou
    /// (RN-052). Separado de <c>Ajuste</c> porque tem causa própria e auditável.
    /// </summary>
    Estorno = 5
}
