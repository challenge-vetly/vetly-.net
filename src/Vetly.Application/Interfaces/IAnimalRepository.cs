using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

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

    /// <summary>
    /// Retorna os animais que um veterinário atendeu ou que estão agendados para ele.
    /// É o escopo de acesso do vet vinculado (RN-105).
    /// </summary>
    Task<IEnumerable<Animal>> ObterPorVeterinarioAsync(Guid veterinarioId);

    /// <summary>
    /// Indica se o veterinário tem alguma consulta com o animal — atendida ou agendada.
    /// </summary>
    Task<bool> VeterinarioAtendeAnimalAsync(Guid veterinarioId, Guid animalId);
}
