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

    /// <summary>Cancela a consulta aplicando a Strategy de reembolso adequada (RN-014/RN-041/RN-042).</summary>
    Task<ResultadoCancelamentoDto> CancelarAsync(Guid id);

    /// <summary>Finaliza a consulta — exige receita veterinária assinada digitalmente (RN-087).</summary>
    Task FinalizarAsync(Guid consultaId);

    /// <summary>Retorna briefing pre-consulta com animal, historico e exames recentes.</summary>
    Task<BriefingConsultaDto> ObterBriefingAsync(Guid consultaId);

    /// <summary>Registra que o veterinario validou o diagnostico (RN-082). Pre-requisito para gerar documentos.</summary>
    Task ValidarDiagnosticoAsync(Guid consultaId);
}
