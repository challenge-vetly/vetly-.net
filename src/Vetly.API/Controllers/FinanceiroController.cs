using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Financeiro;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Consolidado financeiro e liquidacao de repasses (RN-070/RN-071/RN-072).
///
/// Restrito a administracao: o veterinario ve o proprio dinheiro pelo extrato
/// (RN-024), e o Responsavel pelos proprios pagamentos.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FinanceiroController : ControllerBase
{
    private readonly IFinanceiroService _service;

    public FinanceiroController(IFinanceiroService service) => _service = service;

    /// <summary>Consolidado financeiro do periodo (RN-070/RN-072).</summary>
    /// <remarks>
    /// A conta que este painel precisa fechar e uma so: <b>bruto = comissao + repasse
    /// + desconto</b>. O campo <c>fecha</c> diz se ela bate — split incoerente e
    /// silencioso, os totais continuam somando e so a conta cruzada revela o problema.
    ///
    /// <c>porDestinatario</c> e a lista que a operacao usa para pagar, ordenada pela
    /// maior pendencia: um prestador, um valor.
    ///
    /// Sem periodo, o mes corrente — o recorte do fechamento, e o que evita varrer a
    /// base inteira.
    /// </remarks>
    [HttpGet("consolidado")]
    [ProducesResponseType(typeof(ConsolidadoFinanceiroDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterConsolidado(
        [FromQuery] DateTime? inicio = null, [FromQuery] DateTime? fim = null) =>
        Ok(await _service.ObterConsolidadoAsync(inicio, fim));

    /// <summary>Marca os repasses do periodo como liquidados (RN-071).</summary>
    /// <remarks>
    /// A liquidacao acontece fora da plataforma — transferencia, lote do banco. Esta
    /// rota registra que ela aconteceu, e por isso a <c>referencia</c> e obrigatoria:
    /// marcar como pago sem dizer com base em que deixa a conferencia sem ancora.
    ///
    /// Pagamento ja liquidado e ignorado, nao recontado: a operacao repete fechamento
    /// com frequencia, e chamar duas vezes nao pode pagar duas vezes. A resposta diz
    /// quantos foram ignorados.
    ///
    /// <c>destinatarioId</c> nulo liquida todos do periodo — o fechamento do mes.
    /// </remarks>
    [HttpPost("liquidar")]
    [ProducesResponseType(typeof(LiquidacaoRealizadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Liquidar([FromBody] LiquidarRepasseDto dto) =>
        Ok(await _service.LiquidarAsync(dto));
}
