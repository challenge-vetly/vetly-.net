using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Busca;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Busca de prestadores por geolocalizacao e necessidade (RN-001 a RN-033).
/// E a porta de entrada do fluxo 1 do produto.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BuscaController : ControllerBase
{
    private readonly IBuscaService _service;

    public BuscaController(IBuscaService service) => _service = service;

    /// <summary>
    /// Lista clinicas e veterinarios autonomos por proximidade e necessidade,
    /// ordenados pelo score de distancia (40%), avaliacao (30%) e disponibilidade
    /// (30%) — RN-030.
    /// </summary>
    /// <remarks>
    /// A posicao vem do GPS (<c>lat</c>/<c>lng</c>); negada a permissao, informe o
    /// <c>cep</c> como fallback (RN-027). O raio padrao e 10 km, expansivel a 25
    /// (RN-028). A especie do animal e filtro eliminatorio (RN-029).
    ///
    /// Cada item traz a composicao do score, para o app poder explicar a ordem.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(ResultadoBuscaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Buscar(
        [FromQuery] FiltroBuscaDto filtro, [FromQuery] Paginacao paginacao) =>
        Ok(await _service.BuscarAsync(filtro, paginacao));
}
