using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Cancelamento;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.API.Controllers;

/// <summary>
/// Controller de consultas. Máquina de estados: EmCheckout → Confirmada → Realizada,
/// com desvios para Cancelada/NoShowResponsavel/NoShowVeterinario (RN-058/061).
/// Cancelamento aplica Strategy por antecedencia (RN-019/020/021).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConsultasController : ControllerBase
{
    private readonly IConsultaService _service;

    public ConsultasController(IConsultaService service) => _service = service;

    /// <summary>Retorna todas as consultas com filtros opcionais.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ConsultaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTodas(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] Guid? veterinarioId,
        [FromQuery] StatusConsulta? status) =>
        Ok(await _service.ObterTodosAsync(dataInicio, dataFim, veterinarioId, status));

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

    /// <summary>Agenda uma consulta — nasce em EmCheckout, com lock de 10 min (RN-056..059).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConsultaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Agendar([FromBody] CriarConsultaDto dto)
    {
        var criada = await _service.AgendarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    /// <summary>Confirma o pagamento, transicionando EmCheckout → Confirmada (RN-058).</summary>
    [HttpPost("{id:guid}/confirmar-pagamento")]
    [ProducesResponseType(typeof(ConsultaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConfirmarPagamento(Guid id) =>
        Ok(await _service.ConfirmarPagamentoAsync(id));

    /// <summary>Cancela uma consulta aplicando a Strategy de reembolso (RN-019/020/021).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancelar(Guid id)
    {
        var resultado = await _service.CancelarAsync(id);
        return Ok(resultado);
    }

    /// <summary>Marca a consulta como realizada — exige receita assinada digitalmente (RN-031/061).</summary>
    [HttpPost("{id:guid}/realizada")]
    [ProducesResponseType(typeof(ConsultaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MarcarRealizada(Guid id) =>
        Ok(await _service.MarcarRealizadaAsync(id));

    /// <summary>Registra no-show de uma das partes (RN-064/066).</summary>
    [HttpPost("{id:guid}/no-show")]
    [ProducesResponseType(typeof(ConsultaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarNoShow(Guid id, [FromBody] RegistrarNoShowDto dto) =>
        Ok(await _service.RegistrarNoShowAsync(id, dto.Parte));

    /// <summary>Remarca a consulta para uma nova data/hora (RN-022).</summary>
    [HttpPost("{id:guid}/remarcar")]
    [ProducesResponseType(typeof(ConsultaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Remarcar(Guid id, [FromBody] RemarcarConsultaDto dto) =>
        Ok(await _service.RemarcarAsync(id, dto.NovaDataHora));

    /// <summary>Retorna briefing pre-consulta com animal, pré-sintomas, historico e exames recentes.</summary>
    [HttpGet("{id:guid}/briefing")]
    [ProducesResponseType(typeof(BriefingConsultaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterBriefing(Guid id) =>
        Ok(await _service.ObterBriefingAsync(id));

    /// <summary>Registra a validacao manual do diagnostico pelo veterinario (RN-024).</summary>
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
