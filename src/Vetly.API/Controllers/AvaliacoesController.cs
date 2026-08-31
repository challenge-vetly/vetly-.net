using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Avaliacao;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Avaliacao do atendimento e a reputacao que sai dela (RN-055/RN-057).
///
/// So avalia quem foi atendido, e so uma vez por consulta. E o que separa reputacao
/// de campanha: sem o vinculo com um atendimento realizado, a nota vira numero que
/// qualquer um pode empurrar para cima ou para baixo.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AvaliacoesController : ControllerBase
{
    private readonly IAvaliacaoService _service;

    public AvaliacoesController(IAvaliacaoService service) => _service = service;

    /// <summary>Avalia um atendimento realizado (RN-055).</summary>
    /// <remarks>
    /// Somente consulta com status <c>Realizada</c> pode ser avaliada, e o prazo e de
    /// 30 dias — avaliacao muito posterior mede memoria, nao atendimento.
    ///
    /// Segunda avaliacao da mesma consulta devolve 409. A nota nao e editavel depois
    /// de enviada: corrigir avaliacao seria abrir a porta para pressao sobre quem
    /// avaliou.
    /// </remarks>
    [HttpPost("consulta/{consultaId:guid}")]
    [ProducesResponseType(typeof(AvaliacaoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Avaliar(Guid consultaId, [FromBody] CriarAvaliacaoDto dto)
    {
        var avaliacao = await _service.AvaliarAsync(consultaId, dto);

        return CreatedAtAction(
            nameof(ObterReputacao), new { veterinarioId = avaliacao.VeterinarioId }, avaliacao);
    }

    /// <summary>Reputacao de um veterinario, com a distribuicao das notas (RN-057).</summary>
    /// <remarks>
    /// <c>notaPublica</c> e falso abaixo de 3 avaliacoes: uma nota 5 vinda de uma unica
    /// avaliacao nao diz nada sobre o profissional. Enquanto isso, o matching usa o selo
    /// "Novo na Vetly" (RN-033).
    ///
    /// Comentario moderado vem nulo, mas a nota continua contando na media — esconder o
    /// texto nao pode virar um jeito de apagar uma avaliacao ruim.
    /// </remarks>
    [HttpGet("veterinario/{veterinarioId:guid}")]
    [ProducesResponseType(typeof(ReputacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterReputacao(Guid veterinarioId) =>
        Ok(await _service.ObterReputacaoAsync(veterinarioId));

    /// <summary>Resposta publica do veterinario a uma avaliacao (RN-055).</summary>
    /// <remarks>
    /// Uma so: a avaliacao e do Responsavel, e a replica nao vira debate no perfil.
    /// Responde quem foi avaliado, e mais ninguem.
    /// </remarks>
    [HttpPost("{id:guid}/resposta")]
    [ProducesResponseType(typeof(AvaliacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Responder(Guid id, [FromBody] ResponderAvaliacaoDto dto) =>
        Ok(await _service.ResponderAsync(id, dto));

    /// <summary>Esconde o comentario de uma avaliacao por moderacao.</summary>
    /// <remarks>
    /// Exclusivo da administracao, e exige motivo: moderacao sem motivo nao se audita.
    /// A nota continua contando na media.
    /// </remarks>
    [HttpPost("{id:guid}/moderar")]
    [ProducesResponseType(typeof(AvaliacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Moderar(Guid id, [FromBody] ModerarAvaliacaoDto dto) =>
        Ok(await _service.ModerarAsync(id, dto));
}
