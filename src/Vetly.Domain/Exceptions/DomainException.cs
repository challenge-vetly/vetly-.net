namespace Vetly.Domain.Exceptions;

/// <summary>
/// Lançada por métodos de domínio quando uma invariante ou transição de estado é violada.
/// Vive em Vetly.Domain (não em Vetly.Application) porque é lançada de dentro das
/// próprias entidades — o middleware de exceções mapeia esta exceção para HTTP 422,
/// no mesmo formato usado por Application.Exceptions.BusinessRuleException.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Código da regra violada (ex: "CONSULTA-010").</summary>
    public string Codigo { get; }

    public DomainException(string codigo, string mensagem)
        : base(mensagem)
    {
        Codigo = codigo;
    }
}
