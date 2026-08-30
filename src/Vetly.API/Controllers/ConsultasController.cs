using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Cancelamento;
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

    public ConsultasController(IConsultaService service) => _service = service;

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
}
