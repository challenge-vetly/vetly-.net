using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Split;

/// <summary>
/// Strategy de split para veterinários autônomos.
/// Veterinários autônomos recebem uma fatia maior pois não têm empresa intermediária.
/// Percentual padrão: 80% para o veterinário, 20% para a plataforma.
/// </summary>
public class SplitAutonomoStrategy : ISplitFinanceiroStrategy
{
    // Percentual padrão para autônomos — pode ser configurável via appsettings futuramente
    private const decimal PercentualAutonomo = 80m;

    /// <inheritdoc/>
    public bool Aplicavel(Veterinario veterinario) =>
        veterinario.Persona == PersonaVeterinario.Autonomo;

    /// <inheritdoc/>
    public decimal CalcularPercentualVeterinario(Veterinario veterinario, Pagamento pagamento) =>
        PercentualAutonomo;
}
