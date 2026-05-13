namespace Vetly.Application.Exceptions;

/// <summary>
/// Lançada quando um recurso solicitado não existe no banco de dados.
/// O middleware de exceções mapeia esta exceção para HTTP 404.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>Cria a exceção com mensagem descrevendo o recurso não encontrado.</summary>
    public NotFoundException(string recurso, Guid id)
        : base($"{recurso} com id '{id}' não foi encontrado.") { }

    public NotFoundException(string mensagem) : base(mensagem) { }
}
