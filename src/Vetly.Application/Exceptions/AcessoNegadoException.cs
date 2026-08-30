namespace Vetly.Application.Exceptions;

/// <summary>
/// O usuário está autenticado, mas o recurso não pertence ao escopo dele
/// (RN-069/RN-105/RN-106). Mapeada para HTTP 403 pelo middleware.
///
/// Distinta de <see cref="NotFoundException"/> de propósito nas rotas em que o
/// recurso é referenciado por id que o usuário já conhece: responder 404 ali só
/// esconderia o erro, não a existência do dado.
/// </summary>
public class AcessoNegadoException : Exception
{
    /// <summary>Código da regra de negócio violada (ex: "RN-105").</summary>
    public string Codigo { get; }

    public AcessoNegadoException(string codigo, string mensagem) : base(mensagem) =>
        Codigo = codigo;

    /// <summary>Acesso negado por escopo, com a mensagem padrão.</summary>
    public AcessoNegadoException(string codigo = "RN-105")
        : this(codigo, "Este recurso nao pertence ao seu escopo de acesso.") { }
}
