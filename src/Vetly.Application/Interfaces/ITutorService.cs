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

    /// <summary>
    /// Estado das cinco finalidades de consentimento, com datas de concessão e
    /// revogação — o que o Responsável enxerga no app (RN-061).
    /// </summary>
    Task<IEnumerable<ConsentimentoDto>> ObterConsentimentosAsync(Guid tutorId);

    /// <summary>
    /// Concede ou revoga finalidades. O que não vier no corpo permanece como está:
    /// consentimento é granular e um PUT não deve revogar por omissão (RN-061/RN-062).
    /// </summary>
    Task<IEnumerable<ConsentimentoDto>> AtualizarConsentimentosAsync(Guid tutorId, AtualizarConsentimentosDto dto);
}
