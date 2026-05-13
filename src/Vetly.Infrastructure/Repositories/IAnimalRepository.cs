using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Contrato de repositório específico para a entidade <see cref="Animal"/>.
/// </summary>
public interface IAnimalRepository : IRepositoryBase<Animal>
{
    /// <summary>Retorna todos os animais ativos de um tutor.</summary>
    Task<IEnumerable<Animal>> ObterPorTutorAsync(Guid tutorId);

    /// <summary>
    /// Retorna o histórico longitudinal de prontuários de um animal,
    /// ordenado do mais recente para o mais antigo.
    /// </summary>
    Task<IEnumerable<Prontuario>> ObterHistoricoLongitudinalAsync(Guid animalId);

    /// <summary>Retorna todos os exames vinculados a um animal.</summary>
    Task<IEnumerable<Exame>> ObterExamesAsync(Guid animalId);

    /// <summary>Retorna todos os animais ativos cadastrados.</summary>
    Task<IEnumerable<Animal>> ObterAtivosAsync();
}
