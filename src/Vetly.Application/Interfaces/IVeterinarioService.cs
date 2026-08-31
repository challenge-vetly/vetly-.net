using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Veterinario;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de veterinários.</summary>
public interface IVeterinarioService
{
    Task<IEnumerable<VeterinarioDto>> ObterTodosAsync();
    Task<VeterinarioDto> ObterPorIdAsync(Guid id);
    Task<IEnumerable<VeterinarioDto>> ObterPorRegiaoAsync(string uf);
    Task<IEnumerable<ConsultaDto>> ObterAgendaAsync(Guid veterinarioId);
    /// <summary>
    /// Cadastra o veterinário e gera a credencial de primeiro acesso. A senha
    /// temporária volta apenas nesta resposta, para o Admin repassar (P-05).
    /// </summary>
    Task<VeterinarioCriadoDto> CriarAsync(CriarVeterinarioDto dto);
    Task AtualizarAsync(Guid id, CriarVeterinarioDto dto);

    /// <summary>Soft delete — retorna agendamentos futuros afetados (RN-022/RN-025).</summary>
    Task<IEnumerable<ConsultaDto>> DesativarAsync(Guid id);

    /// <summary>
    /// Situação atual do CRMV do veterinário junto ao conselho e reflexo no matching (RN-107).
    /// </summary>
    Task<SituacaoCrmvDto> ObterSituacaoCrmvAsync(Guid id);

    /// <summary>
    /// Reconsulta o conselho regional e reaplica o resultado ao perfil (RN-107).
    /// É o caminho de saída de um perfil que ficou <c>PendenteValidacao</c> por
    /// indisponibilidade do conselho.
    /// </summary>
    Task<ResultadoCrmvDto> RevalidarCrmvAsync(Guid id);

    /// <summary>
    /// Extrato dos atendimentos realizados pelo próprio profissional (RN-024).
    ///
    /// É a única coisa que o veterinário desativado continua alcançando, e por isso
    /// não carrega dado de Responsável, de animal, nem conteúdo clínico: o que ele
    /// precisa é do registro financeiro do próprio trabalho.
    /// </summary>
    Task<ExtratoDoVeterinarioDto> ObterExtratoAsync(DateTime? inicio, DateTime? fim);
}
