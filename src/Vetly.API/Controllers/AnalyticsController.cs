using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Analytics;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Metricas agregadas da plataforma (RN-106).
///
/// Nenhum numero aqui identifica pessoa: analytics e agregado, e cruzar metrica com
/// dado de Responsavel ou de animal seria usar a base clinica para outra coisa.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _service;

    public AnalyticsController(IAnalyticsService service) => _service = service;

    /// <summary>Funil de atendimento, uso da IA e receita do periodo (RN-106).</summary>
    /// <remarks>
    /// Sao tres perguntas, e as secoes respondem uma cada: o agendamento esta virando
    /// atendimento? o dinheiro esta entrando? a IA esta ajudando ou dando trabalho?
    ///
    /// No funil, as taxas importam mais que os absolutos: 30 cancelamentos em 1000
    /// consultas e ruido; em 60, e um problema de agenda.
    ///
    /// Em <c>ia</c>, a metrica que interessa nao e quantos rascunhos foram gerados: e
    /// quantos o veterinario aceitou <b>sem corrigir</b>. Correcao alta significa que a
    /// IA esta dando trabalho em vez de poupar, e recusa alta que ela esta errando o
    /// suficiente para nao ser confiavel. Prontuario manual fica fora do denominador —
    /// nao e rascunho recusado, e atendimento que nunca teve rascunho.
    ///
    /// Sem periodo, os ultimos 30 dias: a janela em que uma metrica ainda reage ao que
    /// foi mudado. Trimestre inteiro esconde a semana ruim.
    /// </remarks>
    [HttpGet("plataforma")]
    [ProducesResponseType(typeof(AnalyticsDaPlataformaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterDaPlataforma(
        [FromQuery] DateTime? inicio = null, [FromQuery] DateTime? fim = null) =>
        Ok(await _service.ObterDaPlataformaAsync(inicio, fim));
}
