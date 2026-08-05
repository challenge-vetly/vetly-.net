using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Fidelidade;
using Vetly.Application.DTOs.Responsavel;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.API.Controllers;

/// <summary>Controller de responsaveis. Gerencia CRUD e listagem de animais do responsavel.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ResponsaveisController : ControllerBase
{
    private readonly IResponsavelService _service;
    private readonly IFidelidadeService _fidelidadeService;

    public ResponsaveisController(IResponsavelService service, IFidelidadeService fidelidadeService)
    {
        _service = service;
        _fidelidadeService = fidelidadeService;
    }

    /// <summary>Retorna todos os responsaveis ativos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ResponsavelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTodos() =>
        Ok(await _service.ObterTodosAsync());

    /// <summary>Retorna um responsavel pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ResponsavelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Retorna todos os animais cadastrados por um responsavel.</summary>
    [HttpGet("{id:guid}/animais")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterAnimais(Guid id) =>
        Ok(await _service.ObterAnimaisAsync(id));

    /// <summary>Cadastra um novo responsavel.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ResponsavelDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarResponsavelDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza dados de um responsavel.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] CriarResponsavelDto dto)
    {
        await _service.AtualizarAsync(id, dto);
        return NoContent();
    }

    /// <summary>Desativa um responsavel (soft delete com anonimizacao LGPD).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desativar(Guid id)
    {
        await _service.DesativarAsync(id);
        return NoContent();
    }

    /// <summary>Lista o historico completo de consentimentos LGPD do responsavel (RN-044, RN-086).</summary>
    [HttpGet("{id:guid}/consentimentos")]
    [ProducesResponseType(typeof(IEnumerable<ConsentimentoLgpdDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarConsentimentos(Guid id) =>
        Ok(await _service.ListarConsentimentosAsync(id));

    /// <summary>Concede um novo consentimento LGPD para a finalidade informada (RN-041/042/043).</summary>
    [HttpPost("{id:guid}/consentimentos")]
    [ProducesResponseType(typeof(ConsentimentoLgpdDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConcederConsentimento(Guid id, [FromBody] ConcederConsentimentoDto dto)
    {
        var concedido = await _service.ConcederConsentimentoAsync(id, dto);
        return CreatedAtAction(nameof(ListarConsentimentos), new { id }, concedido);
    }

    /// <summary>Revoga o consentimento ativo da finalidade informada, preservando o historico (RN-044).</summary>
    [HttpDelete("{id:guid}/consentimentos/{finalidade}")]
    [ProducesResponseType(typeof(ConsentimentoLgpdDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevogarConsentimento(Guid id, FinalidadeConsentimento finalidade) =>
        Ok(await _service.RevogarConsentimentoAsync(id, finalidade));

    /// <summary>Retorna o resumo de fidelidade do responsavel: tier, saldo e progresso (RN-071).</summary>
    [HttpGet("{id:guid}/fidelidade")]
    [ProducesResponseType(typeof(FidelidadeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterFidelidade(Guid id) =>
        Ok(await _fidelidadeService.ObterFidelidadeAsync(id));

    /// <summary>Retorna o extrato completo de lançamentos de pontos de fidelidade (RN-070/074/075).</summary>
    [HttpGet("{id:guid}/fidelidade/extrato")]
    [ProducesResponseType(typeof(IEnumerable<PontosFidelidadeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterExtratoFidelidade(Guid id) =>
        Ok(await _fidelidadeService.ObterExtratoAsync(id));
}
