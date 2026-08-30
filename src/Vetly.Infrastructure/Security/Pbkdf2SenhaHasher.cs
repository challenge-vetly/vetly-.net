using System.Security.Cryptography;
using Vetly.Application.Interfaces;

namespace Vetly.Infrastructure.Security;

/// <summary>
/// Hash de senha com PBKDF2-HMAC-SHA256, usando apenas primitivas da BCL.
///
/// Parâmetros conforme a recomendação do OWASP para PBKDF2-SHA256: 210.000 iterações,
/// salt aleatório de 128 bits por senha e saída de 256 bits.
///
/// O hash é persistido num formato autodescritivo —
/// <c>pbkdf2$sha256$iteracoes$saltBase64$hashBase64</c> — para que aumentar o custo
/// no futuro não invalide as senhas já cadastradas: o verificador lê os parâmetros
/// do próprio registro.
/// </summary>
public class Pbkdf2SenhaHasher : ISenhaHasher
{
    private const int Iteracoes = 210_000;
    private const int TamanhoDoSaltEmBytes = 16;
    private const int TamanhoDoHashEmBytes = 32;
    private const string Algoritmo = "pbkdf2$sha256";

    /// <inheritdoc/>
    public string GerarHash(string senha)
    {
        if (string.IsNullOrWhiteSpace(senha))
            throw new ArgumentException("A senha não pode ser vazia.", nameof(senha));

        var salt = RandomNumberGenerator.GetBytes(TamanhoDoSaltEmBytes);
        var hash = Derivar(senha, salt, Iteracoes);

        return $"{Algoritmo}${Iteracoes}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <inheritdoc/>
    public bool Confere(string senha, string hashArmazenado)
    {
        if (string.IsNullOrWhiteSpace(senha) || string.IsNullOrWhiteSpace(hashArmazenado))
            return false;

        // pbkdf2 $ sha256 $ iteracoes $ salt $ hash
        var partes = hashArmazenado.Split('$');
        if (partes.Length != 5 || partes[0] != "pbkdf2" || partes[1] != "sha256")
            return false;

        if (!int.TryParse(partes[2], out var iteracoes) || iteracoes <= 0)
            return false;

        byte[] salt, esperado;
        try
        {
            salt = Convert.FromBase64String(partes[3]);
            esperado = Convert.FromBase64String(partes[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        var calculado = Derivar(senha, salt, iteracoes, esperado.Length);

        // Comparacao em tempo fixo: comparar byte a byte com short-circuit vazaria,
        // pelo tempo de resposta, quantos bytes iniciais do hash estao corretos.
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }

    private static byte[] Derivar(string senha, byte[] salt, int iteracoes, int tamanho = TamanhoDoHashEmBytes) =>
        Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, tamanho);
}
