using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Fidelidade;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Programa de fidelidade: pontos por consulta realizada e desconto no resgate
/// (RN-051/RN-052).
///
/// O saldo e a soma dos lancamentos, e nao um campo que alguem atualiza. Saldo
/// guardado a parte diverge do extrato no primeiro erro, e ai nao ha como saber qual
/// dos dois esta certo.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FidelidadeController : ControllerBase
{
    private readonly IFidelidadeService _service;

    public FidelidadeController(IFidelidadeService service) => _service = service;

    /// <summary>Saldo de pontos do Responsavel (RN-052).</summary>
    /// <remarks>
    /// Traz tambem quantos pontos vencem nos proximos 30 dias: avisar antes e o que
    /// separa um programa de fidelidade de uma pegadinha.
    ///
    /// O escopo vem do token — o Responsavel alcanca apenas o proprio saldo (RN-106).
    /// </remarks>
    [HttpGet("tutor/{tutorId:guid}/saldo")]
    [ProducesResponseType(typeof(SaldoDePontosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterSaldo(Guid tutorId) =>
        Ok(await _service.ObterSaldoAsync(tutorId));

    /// <summary>Extrato de pontos, do lancamento mais recente ao mais antigo.</summary>
    /// <remarks>
    /// A tabela e append-only: ponto nao e editado nem apagado. Corrigir um credito
    /// indevido e lancar o debito correspondente, como em contabilidade — e por isso o
    /// extrato explica todo movimento do saldo.
    /// </remarks>
    [HttpGet("tutor/{tutorId:guid}/extrato")]
    [ProducesResponseType(typeof(IEnumerable<MovimentoDePontosDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterExtrato(Guid tutorId) =>
        Ok(await _service.ObterExtratoAsync(tutorId));
}
