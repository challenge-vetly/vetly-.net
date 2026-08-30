namespace Vetly.Application.Exceptions;

/// <summary>
/// Lançada quando uma regra de negócio é violada.
/// O middleware de exceções mapeia esta exceção para HTTP 422 (Unprocessable Entity).
/// </summary>
public class BusinessRuleException : Exception
{
    /// <summary>Código da regra de negócio violada (ex: "RN-006").</summary>
    public string Codigo { get; }

    public BusinessRuleException(string codigo, string mensagem)
        : base(mensagem)
    {
        Codigo = codigo;
    }
}
