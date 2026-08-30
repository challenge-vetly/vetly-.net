using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.API.Filters;
using Vetly.Application.DTOs.Dispositivo;
using Vetly.Application.DTOs.Tutor;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>Controller de tutores. Gerencia CRUD e listagem de animais do tutor.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TutoresController : ControllerBase
{
    private readonly ITutorService _service;
    private readonly IDispositivoService _dispositivos;

    public TutoresController(ITutorService service, IDispositivoService dispositivos)
    {
        _service = service;
        _dispositivos = dispositivos;
    }

    /// <summary>
    /// Retorna todos os tutores ativos. Restrito a Admins (RN-069/RN-106): a lista
    /// completa de Responsaveis e dado pessoal, nao pode sair para qualquer autenticado.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(IEnumerable<TutorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterTodos() =>
        Ok(await _service.ObterTodosAsync());

    /// <summary>
    /// Retorna um tutor pelo ID. Isento do portao de consentimento: o Responsavel
    /// precisa conseguir ler o proprio cadastro antes de consentir (RN-060).
    /// </summary>
    [HttpGet("{id:guid}")]
    [IsentoDeConsentimento]
    [ProducesResponseType(typeof(TutorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Retorna todos os animais cadastrados por um tutor.</summary>
    [HttpGet("{id:guid}/animais")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterAnimais(Guid id) =>
        Ok(await _service.ObterAnimaisAsync(id));

    /// <summary>Cadastra um novo tutor.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TutorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarTutorDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza dados de um tutor.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] CriarTutorDto dto)
    {
        await _service.AtualizarAsync(id, dto);
        return NoContent();
    }

    /// <summary>Desativa um tutor (soft delete com anonimizacao LGPD).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desativar(Guid id)
    {
        await _service.DesativarAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Estado das cinco finalidades de consentimento, com as datas de concessao e
    /// revogacao (RN-061). Isento do portao: e a tela que o Responsavel abre antes
    /// de consentir.
    /// </summary>
    [HttpGet("{id:guid}/consentimentos")]
    [IsentoDeConsentimento]
    [ProducesResponseType(typeof(IEnumerable<ConsentimentoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterConsentimentos(Guid id) =>
        Ok(await _service.ObterConsentimentosAsync(id));

    /// <summary>
    /// Concede ou revoga finalidades de consentimento (RN-061/RN-062). O que nao vier
    /// no corpo permanece como esta — um PUT nao revoga por omissao.
    /// A revogacao cessa concessoes futuras e nao apaga registro clinico ja produzido.
    /// </summary>
    [HttpPut("{id:guid}/consentimentos")]
    [IsentoDeConsentimento]
    [ProducesResponseType(typeof(IEnumerable<ConsentimentoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarConsentimentos(
        Guid id, [FromBody] AtualizarConsentimentosDto dto) =>
        Ok(await _service.AtualizarConsentimentosAsync(id, dto));

    /// <summary>Dispositivos ativos do Responsavel, usados para push (RN-007/RN-092).</summary>
    [HttpGet("{id:guid}/dispositivos")]
    [ProducesResponseType(typeof(IEnumerable<DispositivoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObterDispositivos(Guid id) =>
        Ok(await _dispositivos.ObterDoTutorAsync(id));

    /// <summary>
    /// Registra um dispositivo para receber push (RN-007/RN-092).
    /// Idempotente por push token: reinstalar o app reaproveita o registro.
    /// </summary>
    [HttpPost("{id:guid}/dispositivos")]
    [ProducesResponseType(typeof(DispositivoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarDispositivo(Guid id, [FromBody] RegistrarDispositivoDto dto)
    {
        var dispositivo = await _dispositivos.RegistrarAsync(id, dto);
        return Created(string.Empty, dispositivo);
    }

    /// <summary>Remove um dispositivo do Responsavel (remocao logica).</summary>
    [HttpDelete("{id:guid}/dispositivos/{dispositivoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoverDispositivo(Guid id, Guid dispositivoId)
    {
        await _dispositivos.RemoverAsync(id, dispositivoId);
        return NoContent();
    }
}
