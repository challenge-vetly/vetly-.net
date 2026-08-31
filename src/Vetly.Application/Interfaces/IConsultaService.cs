using Vetly.Application.DTOs.Cancelamento;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Consulta;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de consultas.</summary>
public interface IConsultaService
{
    /// <summary>Lista consultas paginadas, aplicando os filtros informados (§2.3, §3.5).</summary>
    Task<ResultadoPaginado<ConsultaDto>> ObterTodosAsync(FiltroConsultaDto filtro, Paginacao paginacao);
    Task<ConsultaDto> ObterPorIdAsync(Guid id);
    Task<IEnumerable<ConsultaDto>> ObterPorVeterinarioAsync(Guid veterinarioId);
    Task<IEnumerable<ConsultaDto>> ObterPorAnimalAsync(Guid animalId);
    Task<ConsultaDto> AgendarAsync(CriarConsultaDto dto);
    Task AtualizarAsync(Guid id, CriarConsultaDto dto);

    /// <summary>
    /// Trava o horário por 10 minutos e cria a consulta em <c>EmCheckout</c>
    /// (RN-003/RN-035). O agendamento só se confirma com o pagamento (RN-006).
    /// </summary>
    Task<CheckoutCriadoDto> IniciarCheckoutAsync(CheckoutDto dto);

    /// <summary>Cancela a consulta aplicando a Strategy de reembolso adequada (RN-014/RN-041/RN-042).</summary>
    Task<ResultadoCancelamentoDto> CancelarAsync(Guid id);

    /// <summary>Finaliza a consulta — exige receita veterinária assinada digitalmente (RN-087).</summary>
    Task FinalizarAsync(Guid consultaId);

    /// <summary>Retorna briefing pre-consulta com animal, historico e exames recentes.</summary>
    Task<BriefingConsultaDto> ObterBriefingAsync(Guid consultaId);

}
