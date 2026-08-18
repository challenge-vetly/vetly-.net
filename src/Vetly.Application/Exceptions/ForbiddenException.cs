namespace Vetly.Application.Exceptions;

/// <summary>
/// Lançada quando o usuário autenticado não tem posse/escopo sobre o recurso solicitado
/// (ex: veterinário tentando acessar consulta de outro veterinário).
/// O middleware de exceções mapeia esta exceção para HTTP 403 (Forbidden).
/// </summary>
public class ForbiddenException : Exception
{
    /// <summary>Código da regra de acesso violada (ex: "ACESSO-002").</summary>
    public string Codigo { get; }

    public ForbiddenException(string codigo, string mensagem)
        : base(mensagem)
    {
        Codigo = codigo;
    }
}
