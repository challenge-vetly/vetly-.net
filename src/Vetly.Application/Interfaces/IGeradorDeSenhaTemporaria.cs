namespace Vetly.Application.Interfaces;

/// <summary>
/// Gera a senha de primeiro acesso do veterinário cadastrado pelo Admin (P-05).
/// </summary>
public interface IGeradorDeSenhaTemporaria
{
    /// <summary>
    /// Gera uma senha aleatória, legível o bastante para ser repassada verbalmente
    /// pelo Admin, e forte o bastante para não ser adivinhada antes da troca.
    /// </summary>
    string Gerar();
}
