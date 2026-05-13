using Vetly.Domain.Entities;

namespace Vetly.Application.Strategies.Split;

/// <summary>
/// Contrato do Strategy Pattern para cálculo do split financeiro.
/// Determina como o valor pago é distribuído entre veterinário e plataforma,
/// variando conforme a persona do veterinário (autônomo ou vinculado a empresa).
/// </summary>
public interface ISplitFinanceiroStrategy
{
    /// <summary>Indica se esta strategy é aplicável ao veterinário informado.</summary>
    bool Aplicavel(Veterinario veterinario);

    /// <summary>
    /// Processa o split e retorna o percentual que será repassado ao veterinário.
    /// A plataforma retém o percentual restante.
    /// </summary>
    decimal CalcularPercentualVeterinario(Veterinario veterinario, Pagamento pagamento);
}
