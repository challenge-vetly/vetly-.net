using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Exame;
using Vetly.Application.DTOs.Prontuario;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de animais.</summary>
public interface IAnimalService
{
    Task<IEnumerable<AnimalDto>> ObterTodosAsync();
    Task<AnimalDto> ObterPorIdAsync(Guid id);
    Task<IEnumerable<ProntuarioDto>> ObterHistoricoAsync(Guid animalId);
    Task<IEnumerable<ExameDto>> ObterExamesAsync(Guid animalId);
    Task<AnimalDto> CriarAsync(CriarAnimalDto dto);
    Task AtualizarAsync(Guid id, CriarAnimalDto dto);
    Task DesativarAsync(Guid id);

    /// <summary>Atualiza o peso do animal (RN-096.2).</summary>
    Task<AnimalDto> AtualizarPesoAsync(Guid id, decimal pesoKg);

    /// <summary>
    /// Oculta um prontuário da visão de veterinários que não o produziram (RN-088).
    /// Lança ANIMAL-002 se o prontuário for classificado como alerta de segurança.
    /// </summary>
    Task OcultarRegistroAsync(Guid animalId, Guid prontuarioId);

    /// <summary>Reexibe um prontuário previamente ocultado.</summary>
    Task ReexibirRegistroAsync(Guid animalId, Guid prontuarioId);
}
