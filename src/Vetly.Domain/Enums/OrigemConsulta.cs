namespace Vetly.Domain.Enums;

/// <summary>
/// Como a consulta entrou na plataforma. Distingue o fluxo do app do atendimento
/// no ato, que tem regra de pagamento própria (RN-040).
/// </summary>
public enum OrigemConsulta
{
    /// <summary>
    /// Agendada pelo app, com slot travado e pagamento antecipado
    /// (RN-006/RN-035) — o caminho normal.
    /// </summary>
    Checkout = 1,

    /// <summary>
    /// Emergência presencial sem agendamento prévio: sem slot, sem lock, e o
    /// pagamento acontece no ato (RN-040).
    /// </summary>
    Emergencia = 2
}
