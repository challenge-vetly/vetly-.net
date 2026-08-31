namespace Vetly.Domain.Enums;

public enum StatusPagamento
{
    Pendente = 1,
    Confirmado = 2,
    Estornado = 3,
    Parcial = 4,

    /// <summary>
    /// Recusado pelo provedor. O horário travado no checkout volta a ficar livre e a
    /// consulta expira (RN-006/RN-035).
    /// </summary>
    Recusado = 5
}
