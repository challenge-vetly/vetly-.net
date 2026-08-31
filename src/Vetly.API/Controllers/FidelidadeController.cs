using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.API.Filters;
using Vetly.Application.DTOs.Fidelidade;
using Vetly.Application.Exceptions;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Programa de fidelidade (RN-046 a RN-054).
///
/// O saldo e a soma dos lancamentos, e nao um campo que alguem atualiza. Saldo
/// guardado a parte diverge do extrato no primeiro erro, e ai nao ha como saber qual
/// dos dois esta certo.
///
/// Todas as rotas sao do proprio Responsavel: o escopo vem do token, e nao ha id de
/// tutor na rota (RN-106).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FidelidadeController : ControllerBase
{
    private readonly IFidelidadeService _service;
    private readonly IUsuarioAtual _usuario;

    public FidelidadeController(IFidelidadeService service, IUsuarioAtual usuario)
    {
        _service = service;
        _usuario = usuario;
    }

    /// <summary>Saldo, tier e o que vence nos proximos 30 dias (RN-048/RN-050).</summary>
    /// <remarks>
    /// O tier sai do que foi <b>creditado</b> nos ultimos 12 meses, nao do saldo: quem
    /// resgatou nao perde a faixa por ter usado o programa — usar e exatamente o
    /// comportamento que o programa quer.
    ///
    /// <c>pontosVencendoEm30Dias</c> conta o que resta de cada lote, nao o credito
    /// original. Avisar antes e o que separa fidelidade de pegadinha.
    /// </remarks>
    [HttpGet("saldo")]
    [ProducesResponseType(typeof(SaldoDePontosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterSaldo() =>
        Ok(await _service.ObterSaldoAsync(TutorDoToken()));

    /// <summary>Extrato de pontos, do lancamento mais recente ao mais antigo.</summary>
    /// <remarks>
    /// Todo movimento do saldo tem lancamento: credito, debito, estorno e expiracao.
    /// Saldo que cai sem lancamento correspondente e saldo que o Responsavel nao
    /// consegue conferir.
    /// </remarks>
    [HttpGet("extrato")]
    [ProducesResponseType(typeof(IEnumerable<MovimentoDePontosDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterExtrato() =>
        Ok(await _service.ObterExtratoAsync(TutorDoToken()));

    /// <summary>Calcula o desconto e a divisao do custo, sem gravar nada (RN-017/RN-051).</summary>
    /// <remarks>
    /// E o que o app mostra antes de o Responsavel confirmar a troca: quanto vale em
    /// reais (100 pontos = R$ 3,00), qual faixa de financiamento se aplica e quanto
    /// cada lado absorve.
    ///
    /// A faixa depende do <b>valor</b> do desconto, nao dos pontos: ate R$ 10 a Vetly
    /// banca sozinha; de R$ 10,01 a R$ 30 a divisao e 60/40; acima de R$ 30, 30/70.
    ///
    /// <c>abatimento</c> vem sempre <c>Simulado</c>: no MVP a divisao e calculada,
    /// gravada e exibida, sem movimentacao real de valores.
    /// </remarks>
    [HttpPost("resgates/simular")]
    [ProducesResponseType(typeof(SimulacaoDeResgateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Simular([FromBody] SimularResgateDto dto) =>
        Ok(await _service.SimularResgateAsync(TutorDoToken(), dto));

    /// <summary>Debita os pontos e emite o cupom (RN-018/RN-050/RN-053).</summary>
    /// <remarks>
    /// O debito consome os lotes em <b>FIFO</b>: o ponto mais antigo primeiro, que e o
    /// que esta mais perto de vencer. Consumir o mais novo faria o Responsavel perder
    /// pontos que ele acabou de usar para pagar.
    ///
    /// O cupom vale 30 dias. <b>Vencido, os pontos nao voltam ao saldo</b> (RN-053) —
    /// e o que evita passivo perpetuo e resgate especulativo, e por isso a validade
    /// vai na resposta e o Responsavel e avisado antes do vencimento.
    ///
    /// A validacao fisica do cupom no estabelecimento nao existe no MVP (RN-019).
    /// </remarks>
    [HttpPost("resgates")]
    [Idempotente]
    [ProducesResponseType(typeof(CupomDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Resgatar([FromBody] SimularResgateDto dto)
    {
        var cupom = await _service.ResgatarAsync(TutorDoToken(), dto);

        return CreatedAtAction(nameof(ObterCupom), new { id = cupom.Id }, cupom);
    }

    /// <summary>Cupons do Responsavel, do mais recente ao mais antigo (RN-053).</summary>
    [HttpGet("cupons")]
    [ProducesResponseType(typeof(IEnumerable<CupomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterCupons() =>
        Ok(await _service.ObterCuponsAsync(TutorDoToken()));

    /// <summary>Um cupom, com o codigo que o app renderiza como QR (RN-053).</summary>
    [HttpGet("cupons/{id:guid}")]
    [ProducesResponseType(typeof(CupomDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterCupom(Guid id) =>
        Ok(await _service.ObterCupomAsync(id));

    /// <summary>
    /// O programa e do Responsável: nao ha id na rota, e o escopo vem do token
    /// (RN-106).
    /// </summary>
    private Guid TutorDoToken() =>
        _usuario.TutorId
        ?? throw new AcessoNegadoException("RN-106",
            "O programa de fidelidade e do Responsavel. Entre com um cadastro de Responsavel.");
}
