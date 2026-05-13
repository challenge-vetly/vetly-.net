using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Contrato de repositório específico para a entidade <see cref="Internacao"/>.</summary>
public interface IInternacaoRepository : IRepositoryBase<Internacao>
{
    /// <summary>Retorna todas as internações de um animal, da mais recente para a mais antiga.</summary>
    Task<IEnumerable<Internacao>> ObterPorAnimalAsync(Guid animalId);

    /// <summary>Retorna a internação ativa de um animal, se existir.</summary>
    Task<Internacao?> ObterAtivaDoAnimalAsync(Guid animalId);
}
