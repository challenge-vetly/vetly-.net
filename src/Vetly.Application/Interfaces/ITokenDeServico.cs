namespace Vetly.Application.Interfaces;

/// <summary>
/// Token que autentica chamadas serviço-a-serviço (§3.6).
///
/// Existe para que os jobs internos possam entrar pelo mesmo caminho que um provedor
/// externo usaria, em vez de haver uma porta de trás que dispensa autenticação.
/// </summary>
public interface ITokenDeServico
{
    /// <summary>Valor do token configurado.</summary>
    string Valor { get; }
}
