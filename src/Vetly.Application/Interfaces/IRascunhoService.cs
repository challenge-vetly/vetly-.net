using Vetly.Application.DTOs.Captura;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Estruturação da consulta pela IA (RN-080, §7.3).
///
/// O que sai daqui é <b>rascunho</b>: nada vira documento sem a decisão explícita do
/// veterinário (RN-082).
/// </summary>
public interface IRascunhoService
{
    /// <summary>
    /// Estrutura a transcrição de uma sessão em prontuário. Roda fora da requisição.
    ///
    /// Falha da IA não trava a consulta: a sessão cai no caminho manual, porque o
    /// atendimento aconteceu e precisa virar prontuário de algum jeito (RN-085).
    /// </summary>
    Task GerarAsync(Guid sessaoCapturaId);

    /// <summary>Rascunho de uma consulta, para a revisão do veterinário (RN-082).</summary>
    Task<RascunhoIaDto> ObterDaConsultaAsync(Guid consultaId);
}
