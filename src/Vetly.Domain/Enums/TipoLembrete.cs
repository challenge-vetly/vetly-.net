namespace Vetly.Domain.Enums;

/// <summary>
/// Assunto da régua de lembretes (§6.3). Valores gravados como número: novos entram
/// no fim.
/// </summary>
public enum TipoLembrete
{
    Vacina = 1,
    Vermifugo = 2,
    Retorno = 3,
    Medicacao = 4,
    CheckUp = 5,

    /// <summary>Banho e tosa — régua aberta por obrigação de higienização (Sprint 4).</summary>
    Higiene = 6
}
