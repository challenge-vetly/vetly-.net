using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Strategies.Split;

/// <summary>
/// Strategy de split para veterinários vinculados a uma empresa.
/// O valor é repartido entre veterinário, empresa e plataforma.
/// Percentual padrão ao veterinário: 60%, empresa: 20%, plataforma: 20%.
/// </summary>
public class SplitEmpresaStrategy : ISplitFinanceiroStrategy
{
    // Veterinários vinculados recebem menos direto pois parte vai à empresa
    private const decimal PercentualVinculado = 60m;

    /// <inheritdoc/>
    public bool Aplicavel(Veterinario veterinario) =>
        veterinario.Persona == PersonaVeterinario.Vinculado;

    /// <inheritdoc/>
    public decimal CalcularPercentualVeterinario(Veterinario veterinario, Pagamento pagamento) =>
        PercentualVinculado;
}
