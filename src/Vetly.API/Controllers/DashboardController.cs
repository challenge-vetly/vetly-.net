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
}
