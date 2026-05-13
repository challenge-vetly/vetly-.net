using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório específico para a entidade <see cref="Exame"/>.</summary>
public interface IExameRepository : IRepositoryBase<Exame>
{
    /// <summary>Retorna todos os exames de um animal.</summary>
    Task<IEnumerable<Exame>> ObterPorAnimalAsync(Guid animalId);

    /// <summary>Retorna todos os exames solicitados por um veterinário.</summary>
    Task<IEnumerable<Exame>> ObterPorVeterinarioAsync(Guid veterinarioId);
}
