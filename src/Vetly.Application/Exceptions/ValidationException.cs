namespace Vetly.Application.Exceptions;

/// <summary>
/// Lançada quando dados de entrada falham em validação.
/// O middleware de exceções mapeia esta exceção para HTTP 400.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>Erros de validação por campo (campo → lista de mensagens).</summary>
    public IDictionary<string, string[]> Erros { get; }

    public ValidationException(string campo, string mensagem)
        : base("Erro de validação.")
    {
        Erros = new Dictionary<string, string[]> { { campo, [mensagem] } };
    }

    public ValidationException(IDictionary<string, string[]> erros)
        : base("Um ou mais erros de validação ocorreram.")
    {
        Erros = erros;
    }
}
