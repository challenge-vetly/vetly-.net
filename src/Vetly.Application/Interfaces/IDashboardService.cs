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

    /// <summary>
    /// Painel consolidado da unidade para o administrador (§5.2): a agenda de todos os
    /// veterinários vinculados e os indicadores operacionais do estabelecimento.
    ///
    /// O id da empresa não vem na rota. O administrador é administrador <b>de uma
    /// unidade</b>, e deixar o cliente escolher qual seria dar a qualquer Admin o
    /// painel de qualquer clínica — exatamente o que a §7.3 veda ao proibir "dados de
    /// outros estabelecimentos".
    /// </summary>
    Task<DashboardDaUnidadeDto> ObterDaUnidadeAsync(DateTime? data);
}
