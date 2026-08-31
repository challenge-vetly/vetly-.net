using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.API.Filters;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.DTOs.Pagamento;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Rotas servico-a-servico (§3.6). Nao sao chamadas pelo app: sao a porta de entrada
/// dos eventos que outros sistemas empurram para a Vetly.
///
/// Autenticacao por token de servico no header <c>X-Vetly-Service-Token</c>, e nao por
/// JWT de usuario — quem chama aqui e um provedor, nao uma pessoa.
/// </summary>
[ApiController]
[Route("api/internos")]
[AllowAnonymous]
[IsentoDeConsentimento]
public class InternosController : ControllerBase
{
    /// <summary>Header que carrega o token de servico.</summary>
    public const string HeaderDoToken = "X-Vetly-Service-Token";

    private readonly IPagamentoService _pagamentos;
    private readonly ICapturaService _captura;
    private readonly IConfiguration _config;
    private readonly ILogger<InternosController> _logger;

    public InternosController(
        IPagamentoService pagamentos, ICapturaService captura,
        IConfiguration config, ILogger<InternosController> logger)
    {
        _pagamentos = pagamentos;
        _captura = captura;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Evento de mudanca de status do pagamento — <b>estado autoritativo</b> da
    /// transacao (RN-006, vetly-tech §7.5).
    /// </summary>
    /// <remarks>
    /// Confirmado: o pagamento e confirmado, a consulta vai de EmCheckout para
    /// Confirmada e o horario e ocupado em definitivo.
    /// Recusado: a consulta expira e o horario volta a ficar livre — segurar o horario
    /// de quem nao pagou tiraria a vaga de quem pagaria (RN-035).
    ///
    /// A rota e idempotente: webhook e entregue mais de uma vez por natureza, e
    /// reentrega de evento ja processado responde 200 sem efeito.
    /// </remarks>
    [HttpPost("pagamentos/webhook")]
    [ProducesResponseType(typeof(ResultadoDoWebhookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> WebhookDePagamento()
    {
        if (!TokenDeServicoConfere())
        {
            _logger.LogWarning("Webhook de pagamento recusado: token de servico invalido ou ausente.");
            return Unauthorized();
        }

        // O payload e lido cru: a validacao da assinatura, num provedor real, e feita
        // sobre os bytes exatos que chegaram, nao sobre o objeto desserializado.
        using var leitor = new StreamReader(Request.Body);
        var payload = await leitor.ReadToEndAsync();

        var token = Request.Headers[HeaderDoToken].ToString();
        var resultado = await _pagamentos.ProcessarWebhookAsync(payload, token);

        return Ok(resultado);
    }

    /// <summary>
    /// Texto devolvido pelo motor de transcricao (RN-009, §5.3).
    /// </summary>
    /// <remarks>
    /// O contrato deste callback e da VETLY, nao do motor: e o que permite trocar de
    /// fornecedor mexendo so dentro do fluxo, sem refazer o caminho de volta.
    ///
    /// Reentrega de um segmento que ja teve desfecho responde 200 sem efeito — nao
    /// duplica texto nem reabre o ciclo.
    /// </remarks>
    [HttpPost("stt/callback")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CallbackDeTranscricao([FromBody] CallbackDeTranscricaoDto dto)
    {
        if (!TokenDeServicoConfere())
        {
            _logger.LogWarning("Callback de transcricao recusado: token de servico invalido ou ausente.");
            return Unauthorized();
        }

        await _captura.RegistrarCallbackAsync(dto);
        return NoContent();
    }

    /// <summary>
    /// Confere o token de servico. Sem token configurado, a rota fica indisponivel —
    /// falhar fechado e melhor que aceitar evento de qualquer origem.
    /// </summary>
    private bool TokenDeServicoConfere()
    {
        var esperado = _config["Servicos:TokenInterno"];

        if (string.IsNullOrWhiteSpace(esperado))
            return false;

        var recebido = Request.Headers[HeaderDoToken].ToString();

        return !string.IsNullOrWhiteSpace(recebido) &&
               System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                   System.Text.Encoding.UTF8.GetBytes(recebido),
                   System.Text.Encoding.UTF8.GetBytes(esperado));
    }
}
