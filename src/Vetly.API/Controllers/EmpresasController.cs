using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Empresa;
using Vetly.Application.DTOs.Repasse;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>Controller de empresas. Gerencia CRUD e vinculo de veterinarios.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmpresasController : ControllerBase
{
    private readonly IEmpresaService _service;

    public EmpresasController(IEmpresaService service) => _service = service;

    /// <summary>Retorna todas as empresas ativas.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmpresaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTodas() =>
        Ok(await _service.ObterTodosAsync());

    /// <summary>Retorna uma empresa pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EmpresaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Retorna os veterinarios vinculados a uma empresa.</summary>
    [HttpGet("{id:guid}/veterinarios")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterVeterinarios(Guid id) =>
        Ok(await _service.ObterVeterinariosAsync(id));

/// <summary>Conta em que a unidade recebe o repasse (§4.1).</summary>
    /// <remarks>
    /// E a conta do <b>estabelecimento</b>, e nao a de nenhum profissional. A §7.3
    /// veda ao administrador os dados bancarios pessoais dos vets vinculados, e e por
    /// isso que as duas contas moram em rotas separadas: esta e do Admin da unidade,
    /// a do veterinario e so dele (<c>GET /api/veterinarios/me/dados-repasse</c>).
    ///
    /// Conta e chave Pix voltam mascaradas, pela mesma razao da rota do veterinario.
    /// </remarks>
    [HttpGet("{id:guid}/dados-repasse")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(DadosDeRepasseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterDadosDeRepasse(Guid id) =>
        Ok(await _service.ObterDadosDeRepasseAsync(id));

    /// <summary>Informa ou substitui a conta de repasse da unidade (§4.1).</summary>
    /// <remarks>
    /// Substitui a conta inteira. O documento do titular aceita CPF ou CNPJ, com ou
    /// sem pontuacao, e comprimento diferente de 11 ou 14 devolve 400.
    ///
    /// No MVP o split segue <b>registrado, nao liquidado</b> (§1.1) — a rota resolve o
    /// destinatario ter endereco de pagamento, nao o pagamento acontecer.
    /// </remarks>
    [HttpPut("{id:guid}/dados-repasse")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(DadosDeRepasseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DefinirDadosDeRepasse(
        Guid id, [FromBody] DefinirDadosDeRepasseDto dto) =>
        Ok(await _service.DefinirDadosDeRepasseAsync(id, dto));

    /// <summary>Cadastra uma nova empresa.</summary>
    [HttpPost]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(EmpresaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarEmpresaDto dto)
    {
        var criada = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
    }

    /// <summary>Vincula um veterinario a uma empresa.</summary>
    [HttpPost("{id:guid}/veterinarios/{veterinarioId:guid}")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VincularVeterinario(Guid id, Guid veterinarioId)
    {
        await _service.VincularVeterinarioAsync(id, veterinarioId);
        return NoContent();
    }

    /// <summary>Atualiza dados de uma empresa.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] CriarEmpresaDto dto)
    {
        await _service.AtualizarAsync(id, dto);
        return NoContent();
    }

    /// <summary>Desativa uma empresa.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desativar(Guid id)
    {
        await _service.DesativarAsync(id);
        return NoContent();
    }
}
