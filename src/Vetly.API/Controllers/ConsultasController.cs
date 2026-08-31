using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.API.Filters;
using Vetly.Application.DTOs.Cancelamento;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Controller de consultas.
/// Agendamento requer pagamento confirmado (RN-006).
/// Cancelamento aplica Strategy por antecedencia (RN-014/RN-041/RN-042).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConsultasController : ControllerBase
{
    private readonly IConsultaService _service;
    private readonly ICapturaService _captura;

    public ConsultasController(IConsultaService service, ICapturaService captura)
    {
        _service = service;
        _captura = captura;
    }

    /// <summary>
    /// Lista consultas com filtros opcionais, paginada (§2.3).
    /// A resposta e o envelope { itens, total, pagina, tamanho }; sem paginacao
    /// informada valem pagina 1 e 20 itens, com teto de 100 por pagina.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ResultadoPaginado<ConsultaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ObterTodas(
        [FromQuery] FiltroConsultaDto filtro,
        [FromQuery] Paginacao paginacao) =>
        Ok(await _service.ObterTodosAsync(filtro, paginacao));

    /// <summary>
    /// Trava o horario por 10 minutos e cria a consulta em EmCheckout (RN-003/RN-035).
    /// O agendamento so se confirma com o pagamento (RN-006).
    /// </summary>
    /// <remarks>
    /// Convive com <c>POST /api/consultas</c>, que segue sendo o caminho da emergencia
    /// presencial e do balcao, onde o pagamento e no ato (RN-040).
    ///
    /// Horario ja reservado por outra pessoa devolve 409: e so escolher outro.
    /// </remarks>
    [HttpPost("checkout")]
    [Idempotente]
    [ProducesResponseType(typeof(CheckoutCriadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> IniciarCheckout([FromBody] CheckoutDto dto)
    {
        var checkout = await _service.IniciarCheckoutAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = checkout.ConsultaId }, checkout);
    }

    /// <summary>Retorna uma consulta pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConsultaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Retorna todas as consultas de um veterinario, opcionalmente filtradas por data.</summary>
    [HttpGet("veterinario/{veterinarioId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<ConsultaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterPorVeterinario(Guid veterinarioId) =>
        Ok(await _service.ObterPorVeterinarioAsync(veterinarioId));

    /// <summary>Retorna todas as consultas de um animal.</summary>
    [HttpGet("animal/{animalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<ConsultaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterPorAnimal(Guid animalId) =>
        Ok(await _service.ObterPorAnimalAsync(animalId));

    /// <summary>Agenda uma consulta (RN-006: pagamento deve estar confirmado).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConsultaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Agendar([FromBody] CriarConsultaDto dto)
    {
        var criada = await _service.AgendarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    /// <summary>Cancela uma consulta aplicando a Strategy de reembolso (RN-014/RN-041/RN-042).</summary>
    [HttpDelete("{id:guid}")]
    [Idempotente]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        var resultado = await _service.CancelarAsync(id);
        return Ok(resultado);
    }

    /// <summary>Finaliza a consulta — exige receita veterinaria assinada digitalmente (RN-087).</summary>
    [HttpPost("{id:guid}/finalizar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Finalizar(Guid id)
    {
        await _service.FinalizarAsync(id);
        return NoContent();
    }

    /// <summary>Retorna briefing pre-consulta com animal, historico e exames recentes.</summary>
    [HttpGet("{id:guid}/briefing")]
    [ProducesResponseType(typeof(BriefingConsultaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterBriefing(Guid id) =>
        Ok(await _service.ObterBriefingAsync(id));

    /// <summary>Registra a validacao manual do diagnostico pelo veterinario (RN-082).</summary>
    [HttpPut("{id:guid}/validar-diagnostico")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ValidarDiagnostico(Guid id)
    {
        await _service.ValidarDiagnosticoAsync(id);
        return NoContent();
    }

    // ── Captura de audio da consulta (RN-008/RN-009/RN-079/RN-085) ───────────

    /// <summary>
    /// Abre a janela de captura: a consulta comeca aqui (RN-008).
    /// </summary>
    /// <remarks>
    /// No plano Basico a consulta inicia normalmente, mas SEM captura (RN-085) — o
    /// prontuario e preenchido manualmente. A resposta traz avisos que o veterinario
    /// precisa ver antes de comecar, como peso ausente, que impede sugestao de dose
    /// (RN-081).
    /// </remarks>
    [HttpPost("{id:guid}/iniciar")]
    [ProducesResponseType(typeof(SessaoIniciadaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Iniciar(Guid id) =>
        Ok(await _captura.IniciarAsync(id));

    /// <summary>
    /// Recebe um trecho de audio da consulta e enfileira a transcricao (RN-009).
    /// </summary>
    /// <remarks>
    /// Fora da janela de captura devolve 409: a IA nao captura audio nem produz
    /// conteudo clinico fora dela (RN-079). O audio ja deve estar no storage — aqui
    /// viaja apenas o midiaId.
    /// </remarks>
    [HttpPost("{id:guid}/captura/segmentos")]
    [ProducesResponseType(typeof(SegmentoRecebidoDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReceberSegmento(Guid id, [FromBody] EnviarSegmentoDto dto) =>
        Accepted(await _captura.ReceberSegmentoAsync(id, dto));

    /// <summary>
    /// Situacao da captura, com o texto ja transcrito ate agora (RN-009).
    /// </summary>
    [HttpGet("{id:guid}/captura")]
    [ProducesResponseType(typeof(EstadoDaCapturaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterCaptura(Guid id) =>
        Ok(await _captura.ObterEstadoAsync(id));

    /// <summary>
    /// Fecha a janela de captura e marca a consulta como realizada (RN-008/RN-038).
    /// </summary>
    /// <remarks>
    /// A partir daqui os processos pos-consulta sao disparados. Segmento que ainda nao
    /// voltou do motor continua sendo aguardado — a consulta nao espera por ele.
    /// </remarks>
    [HttpPost("{id:guid}/encerrar")]
    [ProducesResponseType(typeof(ConsultaEncerradaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Encerrar(Guid id) =>
        Ok(await _captura.EncerrarAsync(id));
}
