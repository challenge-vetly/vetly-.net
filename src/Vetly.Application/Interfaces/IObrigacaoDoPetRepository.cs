using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório para <see cref="ObrigacaoDoPet"/>.</summary>
public interface IObrigacaoDoPetRepository : IRepositoryBase<ObrigacaoDoPet>
{
    /// <summary>Retorna todas as obrigações de um animal, ordenadas por data-limite.</summary>
    Task<IEnumerable<ObrigacaoDoPet>> ObterPorAnimalAsync(Guid animalId);

    /// <summary>Indica se o animal já tem algum calendário de obrigações gerado.</summary>
    Task<bool> ExisteCalendarioAsync(Guid animalId);

    /// <summary>
    /// Retorna a obrigação pendente de um tipo com a data-limite mais próxima, se houver —
    /// usada para casar uma consulta realizada com a obrigação que ela cumpre (RN-070).
    /// </summary>
    Task<ObrigacaoDoPet?> ObterPendenteMaisProximaAsync(Guid animalId, TipoObrigacao tipo);
}
