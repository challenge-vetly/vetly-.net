using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório para <see cref="RegistroOcultado"/>.</summary>
public interface IRegistroOcultadoRepository : IRepositoryBase<RegistroOcultado>
{
    /// <summary>Retorna todos os prontuários ocultados de um animal.</summary>
    Task<IEnumerable<RegistroOcultado>> ObterPorAnimalAsync(Guid animalId);

    /// <summary>Retorna o registro de ocultação de um prontuário específico, se existir.</summary>
    Task<RegistroOcultado?> ObterAsync(Guid animalId, Guid prontuarioId);
}
