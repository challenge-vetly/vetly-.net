using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Dashboard;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Paineis de acompanhamento (RN-105/RN-106).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service) => _service = service;

    /// <summary>Painel do proprio veterinario (RN-105).</summary>
    /// <remarks>
    /// Nao e relatorio: e o que precisa da atencao dele agora. A ordem das secoes
    /// segue a ordem em que as coisas travam — pendencia de documentacao bloqueia
    /// pagamento, agenda define o dia, numeros do mes sao contexto.
    ///
    /// Nao ha id de veterinario na rota: o escopo vem do token, e nem o Admin pede o
    /// painel de outro por aqui.
    ///
    /// <c>data</c> escolhe o dia da agenda; omitido, vale hoje. O mes de referencia
    /// acompanha essa data.
    /// </remarks>
    [HttpGet("veterinario")]
    [ProducesResponseType(typeof(DashboardDoVeterinarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterDoVeterinario([FromQuery] DateTime? data = null) =>
        Ok(await _service.ObterDoVeterinarioAsync(data));

    /// <summary>Painel consolidado da unidade, para o administrador (§5.2).</summary>
    /// <remarks>
    /// A agenda de <b>todos</b> os veterinarios vinculados no dia, mais os indicadores
    /// operacionais do estabelecimento: atendimentos, cancelamentos, no-show e
    /// ocupacao da agenda.
    ///
    /// Nao ha id de empresa na rota. O administrador e administrador de uma unidade, e
    /// deixar o cliente escolher qual daria a qualquer Admin o painel de qualquer
    /// clinica — o "dados de outros estabelecimentos" que a §7.3 veda em letra. A
    /// unidade sai do vinculo do proprio Admin.
    ///
    /// O que <b>nao</b> esta aqui e tao deliberado quanto o que esta: nada de
    /// faturamento, comissao ou repasse. Dinheiro tem rota propria
    /// (<c>GET /api/financeiro/consolidado</c>), com o recorte de periodo e as
    /// vedacoes da §7.3 ja aplicadas. Este painel mostra producao, nunca remuneracao.
    ///
    /// <c>responsaveisNaoResponsivos</c> traz as reguas que esgotaram as tres
    /// tentativas sem resposta (RN-095/§6.4) — dos animais que a unidade atendeu, e
    /// nao da plataforma inteira.
    /// </remarks>
    [HttpGet("unidade")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(DashboardDaUnidadeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterDaUnidade([FromQuery] DateTime? data = null) =>
        Ok(await _service.ObterDaUnidadeAsync(data));
}
