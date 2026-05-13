using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Tutor;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de tutores.</summary>
public interface ITutorService
{
    Task<IEnumerable<TutorDto>> ObterTodosAsync();
    Task<TutorDto> ObterPorIdAsync(Guid id);
    Task<IEnumerable<AnimalDto>> ObterAnimaisAsync(Guid tutorId);
    Task<TutorDto> CriarAsync(CriarTutorDto dto);
    Task AtualizarAsync(Guid id, CriarTutorDto dto);
    Task DesativarAsync(Guid id);
}
