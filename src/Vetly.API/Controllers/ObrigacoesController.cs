using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Obrigacao;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Obrigacoes de cuidado do animal — vacina, vermifugo, antiparasitario, retorno
/// (RN-045/RN-046).
///
/// O Responsavel nao tem como lembrar sozinho de seis reforcos com periodicidades
/// diferentes, e o veterinario so descobre o atraso quando o animal ja voltou doente.
/// O board existe para que a pergunta "esta tudo em dia?" tenha resposta.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ObrigacoesController : ControllerBase
{
    private readonly IObrigacaoService _service;

    public ObrigacoesController(IObrigacaoService service) => _service = service;

    /// <summary>Board de obrigacoes de um animal (RN-045).</summary>
    /// <remarks>
    /// Vencida primeiro, depois vencendo; dentro de cada grupo, a mais antiga na
    /// frente. O board serve para decidir o que fazer, nao para catalogar.
    ///
    /// <c>Vencendo</c> e separado de <c>EmDia</c> porque avisar so no vencimento e
    /// avisar tarde: agendar consulta leva dias. A janela e de 30 dias.
    ///
    /// O escopo vem do token: o Responsavel alcanca os proprios animais, o veterinario
    /// so os que atende (RN-105/RN-106).
    /// </remarks>
    [HttpGet("animal/{animalId:guid}")]
    [ProducesResponseType(typeof(BoardDeObrigacoesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterBoard(
        Guid animalId, [FromQuery] bool incluirArquivadas = false) =>
        Ok(await _service.ObterBoardAsync(animalId, incluirArquivadas));

    /// <summary>Cria uma obrigacao recorrente de cuidado (RN-045).</summary>
    /// <remarks>
    /// A obrigacao guarda a periodicidade, e nao uma data solta: cumprir empurra o
    /// proximo vencimento sozinho. <c>periodicidadeEmDias = 0</c> cria obrigacao de
    /// uma vez so — um retorno pontual, por exemplo.
    /// </remarks>
    [HttpPost("animal/{animalId:guid}")]
    [ProducesResponseType(typeof(ObrigacaoPetDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Criar(Guid animalId, [FromBody] CriarObrigacaoDto dto)
    {
        var obrigacao = await _service.CriarAsync(animalId, dto);

        return CreatedAtAction(nameof(ObterBoard), new { animalId }, obrigacao);
    }

    /// <summary>Registra o cumprimento da obrigacao (RN-045).</summary>
    /// <remarks>
    /// O proximo vencimento conta a partir do <b>cumprimento</b>, nao do vencimento
    /// anterior: quem vacinou com dois meses de atraso nao deve receber o proximo
    /// aviso dois meses adiantado.
    ///
    /// Obrigacao de uma vez so e arquivada ao ser cumprida, em vez de ficar
    /// eternamente vencida no board.
    /// </remarks>
    [HttpPost("{id:guid}/cumprir")]
    [ProducesResponseType(typeof(ObrigacaoPetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cumprir(Guid id, [FromBody] CumprirObrigacaoDto dto) =>
        Ok(await _service.CumprirAsync(id, dto));

    /// <summary>Tira a obrigacao do board sem apagar do historico.</summary>
    /// <remarks>
    /// E o caminho para quando o animal muda de protocolo, ou a vacina deixa de se
    /// aplicar a idade dele.
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(ObrigacaoPetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Arquivar(Guid id) =>
        Ok(await _service.ArquivarAsync(id));

    /// <summary>Cria obrigacoes a partir da carteira de vacinacao ja cadastrada (RN-046).</summary>
    /// <remarks>
    /// Cada tipo de vacina vira uma obrigacao, contada a partir da dose mais recente
    /// daquele tipo — doses antigas do mesmo tipo sao historico, nao obrigacoes
    /// separadas.
    ///
    /// E idempotente: chamar de novo nao duplica o que ja existe. A periodicidade
    /// assumida e anual, e o veterinario ajusta no atendimento.
    /// </remarks>
    [HttpPost("animal/{animalId:guid}/derivar-da-carteira")]
    [ProducesResponseType(typeof(IEnumerable<ObrigacaoPetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DerivarDaCarteira(Guid animalId) =>
        Ok(await _service.DerivarDaCarteiraAsync(animalId));
}
