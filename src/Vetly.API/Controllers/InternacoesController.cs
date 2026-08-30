using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Internacao;

using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>Controller de internacoes. Gerencia abertura, procedimentos diarios e alta.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InternacoesController : ControllerBase
{
    private readonly IInternacaoService _service;

    public InternacoesController(IInternacaoService service) => _service = service;

    /// <summary>Retorna todas as internacoes.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<InternacaoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTodas() =>
        Ok(await _service.ObterTodosAsync());

    /// <summary>Retorna uma internacao pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InternacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Abre uma nova internacao para um animal (valida que nao ha internacao ativa).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(InternacaoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Abrir([FromBody] CriarInternacaoDto dto)
    {
        var criada = await _service.AbrirAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    /// <summary>Registra procedimentos do dia para uma internacao ativa e acumula o valor total apurado (RN-100).</summary>
    [HttpPut("{id:guid}/procedimentos")]
    [ProducesResponseType(typeof(InternacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegistrarProcedimentos(Guid id, [FromBody] RegistrarProcedimentosDto dto) =>
        Ok(await _service.RegistrarProcedimentosAsync(id, dto));

    /// <summary>Concede alta ao animal internado e finaliza a internacao.</summary>
    [HttpPost("{id:guid}/alta")]
    [ProducesResponseType(typeof(AltaInternacaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DarAlta(Guid id)
    {
        var resultado = await _service.DarAltaAsync(id);
        return Ok(resultado);
    }
}
