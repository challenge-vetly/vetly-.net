using Vetly.Application.DTOs.Dashboard;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Painéis de acompanhamento (RN-105/RN-106).
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Painel do próprio veterinário: agenda do dia, o que está travado esperando ele
    /// e os números do mês. O escopo vem do token — não há id na rota.
    /// </summary>
    Task<DashboardDoVeterinarioDto> ObterDoVeterinarioAsync(DateTime? data);
}
