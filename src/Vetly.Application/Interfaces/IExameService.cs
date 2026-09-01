using Vetly.Application.DTOs.Exame;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de exames.</summary>
public interface IExameService
{
    Task<IEnumerable<ExameDto>> ObterTodosAsync();
    Task<ExameDto> ObterPorIdAsync(Guid id);
    Task<ExameDto> CriarAsync(CriarExameDto dto);
    /// <summary>
    /// Registra o resultado e anexa as mídias do laudo (RN-104). Não notifica o
    /// Responsável: o resultado existe, mas ainda não foi liberado.
    /// </summary>
    Task<ExameDto> RegistrarResultadoAsync(Guid id, string resultado, IEnumerable<Guid>? midiaIds = null);
    Task LiberarAoTutorAsync(Guid id);
}
