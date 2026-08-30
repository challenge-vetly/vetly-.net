namespace Vetly.Domain.Enums;

/// <summary>Situação de um pedido na lista de espera (RN-004/RN-037).</summary>
public enum EstadoListaEspera
{
    /// <summary>Na fila, esperando abrir vaga.</summary>
    Aguardando = 1,

    /// <summary>
    /// Vaga oferecida. O primeiro da fila tem prioridade por 15 minutos; passado
    /// isso, a vaga passa ao próximo (RN-037).
    /// </summary>
    Notificado = 2,

    /// <summary>Vaga aceita — seguiu para o checkout.</summary>
    Confirmado = 3,

    /// <summary>A prioridade venceu sem resposta.</summary>
    Expirado = 4,

    /// <summary>O Responsável saiu da fila.</summary>
    Cancelado = 5
}
