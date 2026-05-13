using Vetly.Application.DTOs.Cancelamento;
using Vetly.Application.DTOs.Consulta;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de consultas.</summary>
public interface IConsultaService
{
    Task<IEnumerable<ConsultaDto>> ObterTodosAsync(DateTime? dataInicio, DateTime? dataFim, Guid? veterinarioId, bool? cancelada);
    Task<ConsultaDto> ObterPorIdAsync(Guid id);
    Task<IEnumerable<ConsultaDto>> ObterPorVeterinarioAsync(Guid veterinarioId);
    Task<IEnumerable<ConsultaDto>> ObterPorAnimalAsync(Guid animalId);
    Task<ConsultaDto> AgendarAsync(CriarConsultaDto dto);
    Task AtualizarAsync(Guid id, CriarConsultaDto dto);

    /// <summary>Cancela a consulta aplicando a Strategy de reembolso adequada (RN-019/020/021).</summary>
    Task<ResultadoCancelamentoDto> CancelarAsync(Guid id);
}
