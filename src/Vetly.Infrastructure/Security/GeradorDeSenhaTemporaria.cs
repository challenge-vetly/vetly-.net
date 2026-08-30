using System.Security.Cryptography;
using Vetly.Application.Interfaces;

namespace Vetly.Infrastructure.Security;

/// <summary>
/// Senha temporária de primeiro acesso (P-05).
///
/// Sorteada com <see cref="RandomNumberGenerator"/> — gerador criptográfico, não
/// <c>Random</c>, que é previsível a partir da semente.
///
/// O alfabeto exclui caracteres que se confundem quando alguém dita ou digita a
/// senha (0/O, 1/l/I), porque no MVP ela é repassada na mão pelo Admin.
/// </summary>
public class GeradorDeSenhaTemporaria : IGeradorDeSenhaTemporaria
{
    private const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    private const int Tamanho = 12;

    /// <inheritdoc/>
    public string Gerar() => RandomNumberGenerator.GetString(Alfabeto, Tamanho);
}
