using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Notificacao;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Caixa de entrada de notificacoes do Responsavel (RN-092/RN-093).
///
/// A notificacao e gravada antes de ser enviada, e nao gerada no momento do disparo:
/// o app precisa de uma caixa que sobrevive ao push perdido — aparelho desligado,
/// token trocado, permissao negada.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificacoesController : ControllerBase
{
    private readonly INotificacaoService _service;

    public NotificacoesController(INotificacaoService service) => _service = service;

    /// <summary>Caixa de entrada do Responsavel (RN-092).</summary>
    /// <remarks>
    /// <c>NaoEntregue</c> nao significa perdida: a notificacao segue visivel aqui,
    /// porque push perdido nao pode significar aviso perdido.
    ///
    /// O escopo vem do token — o Responsavel alcanca apenas a propria caixa (RN-106).
    /// </remarks>
    [HttpGet("tutor/{tutorId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<NotificacaoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterCaixaDeEntrada(
        Guid tutorId, [FromQuery] bool apenasNaoLidas = false) =>
        Ok(await _service.ObterCaixaDeEntradaAsync(tutorId, apenasNaoLidas));

    /// <summary>O que o Responsavel escolheu receber (RN-093).</summary>
    /// <remarks>
    /// So ha uma escolha porque so ha um tipo opcional. Aviso de consulta, documento
    /// publicado e obrigacao vencendo sao o servico contratado — oferece-los como
    /// preferencia faria o app poder deixar de avisar sobre a saude do animal.
    ///
    /// O escopo vem do token: nao ha parametro de Responsavel aqui, e nao pode haver.
    /// </remarks>
    [HttpGet("preferencias")]
    [ProducesResponseType(typeof(PreferenciasDeNotificacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterPreferencias() =>
        Ok(await _service.ObterPreferenciasAsync());

    /// <summary>Liga ou desliga as comunicacoes promocionais (RN-093).</summary>
    /// <remarks>
    /// Opt-in, nunca opt-out: quem nunca escolheu nao recebe promocao. A escolha e
    /// gravada no registro de consentimento, que e o que vale juridicamente (RN-061).
    /// </remarks>
    [HttpPut("preferencias")]
    [ProducesResponseType(typeof(PreferenciasDeNotificacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarPreferencias([FromBody] AtualizarPreferenciasDto dto) =>
        Ok(await _service.AtualizarPreferenciasAsync(dto));

    /// <summary>Registra que o Responsavel abriu a notificacao no app.</summary>
    /// <remarks>
    /// A primeira leitura e a que fica: e o dado que diz se o aviso chegou a quem
    /// cuida do animal.
    /// </remarks>
    [HttpPost("{id:guid}/lida")]
    [ProducesResponseType(typeof(NotificacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarComoLida(Guid id) =>
        Ok(await _service.MarcarComoLidaAsync(id));
}
