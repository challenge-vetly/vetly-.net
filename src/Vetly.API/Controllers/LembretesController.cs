using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Lembrete;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>
/// Controller de lembretes agendados.
/// Regua de contato com alerta a clinica apos 3 tentativas sem resposta (RN-029/030).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LembretesController : ControllerBase
{
    private readonly ILembreteService _service;

    public LembretesController(ILembreteService service) => _service = service;

    /// <summary>Agenda um lembrete para um responsavel sobre evento do animal (vacina, retorno, medicacao…).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(LembreteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Agendar([FromBody] CriarLembreteDto dto)
    {
        var lembrete = await _service.AgendarLembreteAsync(dto.AnimalId, dto.ResponsavelId, dto.Tipo, dto.DataEvento);
        return CreatedAtAction(nameof(RegistrarTentativa), new { id = lembrete.Id }, MapearParaDto(lembrete));
    }

    /// <summary>Registra uma tentativa de contato. Apos 3 tentativas sem resposta, alerta e enviado a clinica (RN-030).</summary>
    [HttpPost("{id:guid}/tentativa")]
    [ProducesResponseType(typeof(LembreteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarTentativa(Guid id)
    {
        var lembrete = await _service.ProcessarTentativaAsync(id);
        return Ok(MapearParaDto(lembrete));
    }

    /// <summary>Registra a resposta do responsavel, encerrando a regua de contato (RN-029).</summary>
    [HttpPost("{id:guid}/resposta")]
    [ProducesResponseType(typeof(LembreteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarResposta(Guid id)
    {
        var lembrete = await _service.RegistrarRespostaAsync(id);
        return Ok(MapearParaDto(lembrete));
    }

    private static LembreteDto MapearParaDto(Domain.Entities.LembreteAgendado l) => new()
    {
        Id = l.Id,
        AnimalId = l.AnimalId,
        ResponsavelId = l.ResponsavelId,
        Tipo = l.Tipo,
        DataEvento = l.DataEvento,
        TentativasRealizadas = l.TentativasRealizadas,
        ResponsavelRespondeu = l.ResponsavelRespondeu,
        AlertaEnviadoClinica = l.AlertaEnviadoClinica
    };
}
