using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato de repositório da agenda: configuração, horários e serviços
/// (RN-032/RN-034/RN-035).
/// </summary>
public interface IAgendaRepository
{
    /// <summary>Configuração de agenda do veterinário, se já existir.</summary>
    Task<AgendaConfig?> ObterConfigAsync(Guid veterinarioId);

    /// <summary>Registra a configuração de agenda.</summary>
    Task AdicionarConfigAsync(AgendaConfig config);

    /// <summary>Atualiza a configuração de agenda.</summary>
    void AtualizarConfig(AgendaConfig config);

    /// <summary>Horários do veterinário dentro de um intervalo, em ordem cronológica.</summary>
    Task<IEnumerable<Slot>> ObterSlotsAsync(Guid veterinarioId, DateTime de, DateTime ate);

    /// <summary>
    /// Instantes de início já materializados para o veterinário a partir de uma data.
    /// Serve para materializar de novo sem duplicar horário.
    /// </summary>
    Task<HashSet<DateTime>> ObterIniciosMaterializadosAsync(Guid veterinarioId, DateTime de);

    /// <summary>Um horário específico.</summary>
    Task<Slot?> ObterSlotAsync(Guid slotId);

    /// <summary>Registra os horários materializados.</summary>
    Task AdicionarSlotsAsync(IEnumerable<Slot> slots);

    /// <summary>Atualiza um horário.</summary>
    void AtualizarSlot(Slot slot);

    /// <summary>
    /// Conta os horários disponíveis do veterinário nas próximas 48 horas.
    /// É o fator de disponibilidade do score de matching (RN-030/RN-031).
    /// </summary>
    Task<Dictionary<Guid, int>> ContarDisponiveisNasProximas48hAsync(IEnumerable<Guid> veterinarioIds, DateTime agora);

    /// <summary>
    /// Próximo horário livre de cada veterinário, a partir de agora. É o que o
    /// resultado da busca exibe e o que sustenta o filtro "atende hoje" (RN-032).
    /// </summary>
    Task<Dictionary<Guid, DateTime>> ObterProximoHorarioLivreAsync(IEnumerable<Guid> veterinarioIds, DateTime agora);

    /// <summary>Serviços ativos de um prestador.</summary>
    Task<IEnumerable<Servico>> ObterServicosAsync(Guid prestadorId);

    /// <summary>Um serviço específico.</summary>
    Task<Servico?> ObterServicoAsync(Guid servicoId);

    /// <summary>Registra um serviço.</summary>
    Task AdicionarServicoAsync(Servico servico);

    /// <summary>Atualiza um serviço.</summary>
    void AtualizarServico(Servico servico);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task<int> SalvarAsync();
}
