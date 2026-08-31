namespace Vetly.Domain.Enums;

/// <summary>
/// O que está sendo cobrado. A internação é a exceção ao pagamento antecipado:
/// cobra caução na entrada e o saldo na saída (RN-101).
/// </summary>
public enum TipoPagamento
{
    /// <summary>Consulta ou serviço avulso, pago no agendamento (RN-006).</summary>
    Consulta = 1,

    /// <summary>Caução cobrada na entrada da internação (RN-101).</summary>
    Caucao = 2,

    /// <summary>Saldo apurado na alta da internação (RN-101/RN-102).</summary>
    SaldoInternacao = 3
}
