using Vetly.Application.DTOs.Agenda;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato do serviço de agenda do veterinário (RN-032/RN-034/RN-035).
/// </summary>
public interface IAgendaService
{
    /// <summary>
    /// Configura a agenda e materializa os horários dos próximos 60 dias (RN-034).
    /// Rematerializar não duplica horário nem desfaz agendamento existente.
    /// </summary>
    Task<AgendaConfigDto> ConfigurarAsync(Guid veterinarioId, ConfigurarAgendaDto dto);

    /// <summary>Configuração de agenda vigente.</summary>
    Task<AgendaConfigDto> ObterConfigAsync(Guid veterinarioId);

    /// <summary>
    /// Horários livres do veterinário no período, agrupados por dia. É o que o
    /// Responsável vê ao escolher o horário (RN-034/RN-035).
    /// </summary>
    Task<DisponibilidadeDto> ObterDisponibilidadeAsync(Guid veterinarioId, DateTime? de = null, DateTime? ate = null);

    /// <summary>Serviços ativos do prestador (RN-032).</summary>
    Task<IEnumerable<ServicoDto>> ObterServicosAsync(Guid prestadorId);

    /// <summary>
    /// Define a vitrine de serviços do prestador. Serviço que sai da lista é
    /// desativado, não apagado — consulta antiga aponta para ele.
    /// </summary>
    Task<IEnumerable<ServicoDto>> DefinirServicosAsync(Guid prestadorId, DefinirServicosDto dto);
}
