using Vetly.Application.DTOs.Cancelamento;
using Vetly.Application.DTOs.Consulta;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de consultas.</summary>
public interface IConsultaService
{
    Task<IEnumerable<ConsultaDto>> ObterTodosAsync(DateTime? dataInicio, DateTime? dataFim, Guid? veterinarioId, StatusConsulta? status);
    Task<ConsultaDto> ObterPorIdAsync(Guid id);
    Task<IEnumerable<ConsultaDto>> ObterPorVeterinarioAsync(Guid veterinarioId);
    Task<IEnumerable<ConsultaDto>> ObterPorAnimalAsync(Guid animalId);

    /// <summary>Agenda uma consulta, criando-a em EmCheckout com lock de 10 min (RN-056..059).</summary>
    Task<ConsultaDto> AgendarAsync(CriarConsultaDto dto);

    /// <summary>Confirma o pagamento, transicionando EmCheckout → Confirmada (RN-058).</summary>
    Task<ConsultaDto> ConfirmarPagamentoAsync(Guid consultaId);

    /// <summary>Cancela a consulta aplicando a Strategy de reembolso adequada (RN-019/020/021).</summary>
    Task<ResultadoCancelamentoDto> CancelarAsync(Guid id);

    /// <summary>
    /// Cancela a consulta por iniciativa do veterinário: crédito de cortesia (10% do
    /// valor, teto R$ 30) + strike de reputação (RN-065/067).
    /// </summary>
    Task<CancelamentoPeloVeterinarioDto> CancelamentoPeloVeterinarioAsync(Guid consultaId);

    /// <summary>
    /// Marca a consulta como realizada (RN-061) — exige receita assinada digitalmente (RN-031)
    /// e que o chamador seja o veterinário responsável.
    /// </summary>
    Task<ConsultaDto> MarcarRealizadaAsync(Guid consultaId);

    /// <summary>Registra no-show de uma das partes (RN-064/066).</summary>
    Task<ConsultaDto> RegistrarNoShowAsync(Guid consultaId, ParteNoShow parte);

    /// <summary>Remarca a consulta para uma nova data/hora, incrementando o contador (RN-022).</summary>
    Task<ConsultaDto> RemarcarAsync(Guid consultaId, DateTime novaDataHora);

    /// <summary>Retorna briefing pre-consulta com animal, historico, pré-sintomas e exames recentes.</summary>
    Task<BriefingConsultaDto> ObterBriefingAsync(Guid consultaId);

    /// <summary>Registra que o veterinario validou o diagnostico (RN-024). Pre-requisito para gerar documentos.</summary>
    Task ValidarDiagnosticoAsync(Guid consultaId);
}
