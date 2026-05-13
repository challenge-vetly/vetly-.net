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
}
