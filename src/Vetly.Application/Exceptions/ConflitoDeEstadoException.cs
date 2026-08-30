namespace Vetly.Application.Exceptions;

/// <summary>
/// O recurso existe e o pedido é legítimo, mas o estado atual não permite a
/// operação — o horário foi tomado por outro checkout, o limite de remarcações
/// acabou, a avaliação já existe. Mapeada para HTTP 409 (§2.4).
///
/// Distinta de <see cref="BusinessRuleException"/> (422) de propósito: 409 diz ao
/// cliente que <b>tentar de novo com outro recurso</b> resolve, enquanto 422 diz que
/// o pedido em si viola uma regra.
/// </summary>
public class ConflitoDeEstadoException : Exception
{
    /// <summary>Código da regra de negócio envolvida (ex: "RN-035").</summary>
    public string Codigo { get; }

    public ConflitoDeEstadoException(string codigo, string mensagem) : base(mensagem) =>
        Codigo = codigo;
}
