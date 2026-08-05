using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade <see cref="Animal"/>.
/// </summary>
public interface IAnimalRepository : IRepositoryBase<Animal>
{
    /// <summary>Retorna todos os animais ativos de um responsavel.</summary>
    Task<IEnumerable<Animal>> ObterPorResponsavelAsync(Guid responsavelId);

    /// <summary>
    /// Retorna o histórico longitudinal de prontuários de um animal,
    /// ordenado do mais recente para o mais antigo.
    /// </summary>
    Task<IEnumerable<Prontuario>> ObterHistoricoLongitudinalAsync(Guid animalId);

    /// <summary>Retorna todos os exames vinculados a um animal.</summary>
    Task<IEnumerable<Exame>> ObterExamesAsync(Guid animalId);

    /// <summary>Retorna todos os animais ativos cadastrados.</summary>
    Task<IEnumerable<Animal>> ObterAtivosAsync();

    /// <summary>Retorna um único prontuário pelo Id, ou nulo se não existir.</summary>
    Task<Prontuario?> ObterProntuarioPorIdAsync(Guid prontuarioId);
}
