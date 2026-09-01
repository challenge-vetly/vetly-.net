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

    /// <summary>
    /// O que aconteceria se a consulta fosse cancelada agora, sem executar nada
    /// (RN-014/RN-041/RN-042). O Responsável precisa ver o valor antes de confirmar.
    /// </summary>
    Task<SimulacaoDeCancelamentoDto> SimularCancelamentoAsync(Guid consultaId);

    /// <summary>
    /// Registra os pré-sintomas do agendamento (RN-005/RN-036). Alimentam o briefing
    /// e o contexto da IA, e por isso só valem antes do atendimento.
    /// </summary>
    Task RegistrarPreSintomasAsync(Guid consultaId, PreSintomasDto dto);

    /// <summary>
    /// Transfere a consulta para outro horário sem nova cobrança, até o limite de 2
    /// remarcações (RN-013/RN-043).
    /// </summary>
    Task<RemarcacaoRealizadaDto> RemarcarAsync(Guid consultaId, RemarcarConsultaDto dto);

    /// <summary>
    /// Agenda o retorno de um atendimento já realizado (RN-013/RN-090).
    ///
    /// Sem cobrança nova: o retorno é a segunda metade de um tratamento já pago. Quem
    /// o marca é o profissional que conduziu o caso, ao fim da consulta.
    /// </summary>
    Task<RetornoAgendadoDto> AgendarRetornoAsync(Guid consultaId, AgendarRetornoDto dto);

    /// <summary>
    /// Registra o não comparecimento do Responsável (RN-044). Sem reembolso, seguindo
    /// a faixa "menos de 2h ou no ato" da RN-014.
    /// </summary>
    Task<NoShowRegistradoDto> RegistrarNoShowAsync(Guid consultaId);

    /// <summary>Retorna briefing pre-consulta com animal, historico e exames recentes.</summary>
    Task<BriefingConsultaDto> ObterBriefingAsync(Guid consultaId);

}
