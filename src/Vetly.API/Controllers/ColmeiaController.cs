using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Colmeia;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Colmeia: o historico do animal atravessando clinicas, sob autorizacao do
/// Responsavel (RN-090/RN-105).
///
/// Quem concede e o Responsavel, quem usa e o veterinario, e todo acesso fica
/// registrado. As tres coisas andam juntas: autorizacao sem registro seria um cheque
/// em branco, e registro sem autorizacao nao seria acesso, seria vazamento.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ColmeiaController : ControllerBase
{
    private readonly IColmeiaService _service;

    public ColmeiaController(IColmeiaService service) => _service = service;

    /// <summary>Concede a um veterinario acesso ao historico do animal (RN-090).</summary>
    /// <remarks>
    /// So o Responsavel concede: o historico e dele, e a clinica que quisesse se
    /// autoconceder acesso e exatamente o que esta guarda impede.
    ///
    /// A concessao nasce com prazo — 30 dias por padrao, 365 no maximo. Acesso clinico
    /// que nao expira sozinho e acesso que ninguem lembra de revogar.
    ///
    /// Ja havendo autorizacao vigente para o mesmo veterinario neste animal, devolve
    /// 409 em vez de renovar em silencio: o Responsavel veria duas autorizacoes e nao
    /// saberia qual vale.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(AcessoColmeiaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Conceder([FromBody] ConcederAcessoDto dto)
    {
        var acesso = await _service.ConcederAsync(dto);

        return CreatedAtAction(nameof(ListarDoAnimal), new { animalId = acesso.AnimalId }, acesso);
    }

    /// <summary>Revoga uma autorizacao (RN-062/RN-090).</summary>
    /// <remarks>
    /// Revogar nao apaga o que ja foi acessado: o log continua, e e isso que o
    /// Responsavel precisa poder conferir depois.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(AcessoColmeiaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revogar(Guid id) =>
        Ok(await _service.RevogarAsync(id));

    /// <summary>Autorizacoes de um animal — quem alcanca o que (RN-090).</summary>
    [HttpGet("animal/{animalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<AcessoColmeiaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarDoAnimal(Guid animalId) =>
        Ok(await _service.ListarDoAnimalAsync(animalId));

    /// <summary>Acessos efetivamente feitos ao historico do animal (RN-090).</summary>
    /// <remarks>
    /// E a contrapartida da autorizacao: o Responsavel ve quem leu o que e quando.
    /// Tentativa negada tambem aparece — e justamente o que se quer enxergar numa
    /// auditoria.
    /// </remarks>
    [HttpGet("animal/{animalId:guid}/acessos")]
    [ProducesResponseType(typeof(IEnumerable<LogAcessoColmeiaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterAcessos(Guid animalId) =>
        Ok(await _service.ObterLogDoAnimalAsync(animalId));
}
