namespace Vetly.Application.Interfaces;

/// <summary>
/// Porta para o hash de senhas. A senha em claro nunca sai desta fronteira:
/// serviços e entidades trabalham apenas com o hash.
/// </summary>
public interface ISenhaHasher
{
    /// <summary>Gera o hash de uma senha, com salt aleatório por senha.</summary>
    string GerarHash(string senha);

    /// <summary>
    /// Confere uma senha contra o hash armazenado. A comparação é feita em tempo
    /// constante, para não vazar informação por diferença de tempo de resposta.
    /// </summary>
    bool Confere(string senha, string hashArmazenado);
}
