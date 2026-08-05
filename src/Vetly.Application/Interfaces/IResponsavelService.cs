using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Responsavel;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de responsaveis.</summary>
public interface IResponsavelService
{
    Task<IEnumerable<ResponsavelDto>> ObterTodosAsync();
    Task<ResponsavelDto> ObterPorIdAsync(Guid id);
    Task<IEnumerable<AnimalDto>> ObterAnimaisAsync(Guid responsavelId);
    Task<ResponsavelDto> CriarAsync(CriarResponsavelDto dto);
    Task AtualizarAsync(Guid id, CriarResponsavelDto dto);
    Task DesativarAsync(Guid id);
}
