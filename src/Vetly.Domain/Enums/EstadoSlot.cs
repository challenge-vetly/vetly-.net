namespace Vetly.Domain.Enums;

/// <summary>
/// Estado de um horário na agenda (RN-035, RN-037).
/// Máquina: <c>Livre → EmCheckout (lock de 10 min) → Confirmado</c>, ou de volta a
/// <c>Livre</c> se o lock expira ou o pagamento é recusado.
/// </summary>
public enum EstadoSlot
{
    /// <summary>Disponível para agendamento.</summary>
    Livre = 1,

    /// <summary>Reservado temporariamente durante o checkout — o lock vale 10 minutos.</summary>
    EmCheckout = 2,

    /// <summary>Ocupado por consulta confirmada.</summary>
    Confirmado = 3,

    /// <summary>Bloqueado pelo próprio veterinário (folga, compromisso).</summary>
    Bloqueado = 4
}
