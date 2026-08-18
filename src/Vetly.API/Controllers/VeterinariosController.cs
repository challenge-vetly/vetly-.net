using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Avaliacao;
using Vetly.Application.DTOs.Prontuario;
using Vetly.Application.DTOs.Veterinario;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>Controller de veterinarios. Gerencia CRUD, busca por regiao, agenda e vinculo com empresa.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VeterinariosController : ControllerBase
{
    private readonly IVeterinarioService _service;
    private readonly IAcessoProntuarioService _acessoProntuarioService;
    private readonly IAvaliacaoService _avaliacaoService;
    private readonly TimeProvider _timeProvider;

    public VeterinariosController(
        IVeterinarioService service, IAcessoProntuarioService acessoProntuarioService,
        IAvaliacaoService avaliacaoService, TimeProvider timeProvider)
    {
        _service = service;
        _acessoProntuarioService = acessoProntuarioService;
        _avaliacaoService = avaliacaoService;
        _timeProvider = timeProvider;
    }

    /// <summary>Retorna todos os veterinarios ativos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<VeterinarioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTodos() =>
        Ok(await _service.ObterTodosAsync());

    /// <summary>Retorna um veterinario pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VeterinarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Retorna veterinarios de uma UF (ex: SP, RJ).</summary>
    [HttpGet("regiao/{uf}")]
    [ProducesResponseType(typeof(IEnumerable<VeterinarioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterPorRegiao(string uf) =>
        Ok(await _service.ObterPorRegiaoAsync(uf));

    /// <summary>Retorna a agenda futura de consultas de um veterinario.</summary>
    [HttpGet("{id:guid}/agenda")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterAgenda(Guid id) =>
        Ok(await _service.ObterAgendaAsync(id));

    /// <summary>Cadastra um novo veterinario (RN-011: CRMV validado). Restrito a Admins.</summary>
    [HttpPost]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(VeterinarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarVeterinarioDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza dados de um veterinario existente.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] CriarVeterinarioDto dto)
    {
        await _service.AtualizarAsync(id, dto);
        return NoContent();
    }

    /// <summary>Desativa um veterinario (soft delete — RN-008). Restrito a Admins.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desativar(Guid id)
    {
        var agendamentos = await _service.DesativarAsync(id);
        return Ok(agendamentos);
    }

    /// <summary>Retorna as concessoes de acesso a prontuario ativas do veterinario (RN-083 — uso administrativo).</summary>
    [HttpGet("{id:guid}/concessoes")]
    [ProducesResponseType(typeof(IEnumerable<ConcessaoAcessoProntuarioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterConcessoes(Guid id) =>
        Ok(await _acessoProntuarioService.ObterConcessoesAtivasAsync(id, _timeProvider.GetUtcNow().UtcDateTime));

    /// <summary>Retorna as avaliações não invalidadas recebidas pelo veterinario (RN-076..081).</summary>
    [HttpGet("{id:guid}/avaliacoes")]
    [ProducesResponseType(typeof(IEnumerable<AvaliacaoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterAvaliacoes(Guid id) =>
        Ok(await _avaliacaoService.ObterPorVeterinarioAsync(id));
}
