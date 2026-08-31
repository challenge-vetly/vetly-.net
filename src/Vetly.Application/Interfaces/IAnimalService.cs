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

    /// <summary>
    /// Board do pet: obrigações, próximos agendamentos, documentos recentes e o estado
    /// do avatar (RN-011/RN-020/RN-090/RN-096).
    /// </summary>
    Task<BoardDoPetDto> ObterBoardAsync(Guid animalId);
    Task<IEnumerable<ExameDto>> ObterExamesAsync(Guid animalId);
    Task<AnimalDto> CriarAsync(CriarAnimalDto dto);
    Task AtualizarAsync(Guid id, CriarAnimalDto dto);
    Task DesativarAsync(Guid id);
}
