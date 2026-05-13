using Vetly.Application.DTOs.Exame;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de exames.</summary>
public interface IExameService
{
    Task<IEnumerable<ExameDto>> ObterTodosAsync();
    Task<ExameDto> ObterPorIdAsync(Guid id);
    Task<ExameDto> CriarAsync(CriarExameDto dto);
    Task<ExameDto> RegistrarResultadoAsync(Guid id, string resultado);
    Task LiberarAoTutorAsync(Guid id);
}
