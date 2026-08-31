using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Agenda;
using Vetly.Application.DTOs.Veterinario;
using Vetly.API.Filters;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>Controller de veterinarios. Gerencia CRUD, busca por regiao, agenda e vinculo com empresa.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VeterinariosController : ControllerBase
{
    private readonly IVeterinarioService _service;
    private readonly IAgendaService _agenda;

    public VeterinariosController(IVeterinarioService service, IAgendaService agenda)
    {
        _service = service;
        _agenda = agenda;
    }

    /// <summary>
    /// Extrato dos atendimentos realizados pelo proprio veterinario (RN-024).
    /// </summary>
    /// <remarks>
    /// E a unica rota de negocio que o veterinario desativado continua alcancando, e o
    /// formato segue disso: <b>sem nome de Responsavel, sem nome de animal, sem
    /// conteudo clinico</b>. O que ele precisa e do registro financeiro do proprio
    /// trabalho — conferir repasses, fechar a contabilidade, sustentar uma eventual
    /// disputa. Nada disso exige saber de quem era o pet, e dado clinico aqui seria
    /// dado vazando por uma porta que a RN-022 fechou.
    ///
    /// Nao ha id de veterinario na rota: o escopo vem do token, e o extrato e sempre o
    /// do proprio profissional (RN-105).
    ///
    /// Sem periodo informado, os ultimos 12 meses. Consulta cancelada ou expirada
    /// aparece na lista, para conferencia, mas nao soma dinheiro que nao existiu.
    /// </remarks>
    [HttpGet("me/extrato")]
    [PermitidoAoVetDesativado]
    [ProducesResponseType(typeof(ExtratoDoVeterinarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterExtrato(
        [FromQuery] DateTime? inicio = null, [FromQuery] DateTime? fim = null) =>
        Ok(await _service.ObterExtratoAsync(inicio, fim));

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

    /// <summary>
    /// Cadastra um novo veterinario (RN-107: CRMV validado junto ao conselho).
    /// Restrito a Admins.
    /// </summary>
    /// <remarks>
    /// A resposta traz a SENHA TEMPORARIA de primeiro acesso do profissional. Ela
    /// aparece uma unica vez, aqui — nao volta em nenhuma outra rota. O Admin repassa
    /// ao veterinario, que a troca em POST /api/auth/trocar-senha (pendencia P-05).
    /// </remarks>
    [HttpPost]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(VeterinarioCriadoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar([FromBody] CriarVeterinarioDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Veterinario.Id }, criado);
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

    /// <summary>
    /// Situacao do CRMV junto ao conselho regional e reflexo no matching (RN-107).
    /// </summary>
    [HttpGet("{id:guid}/crmv")]
    [ProducesResponseType(typeof(SituacaoCrmvDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterSituacaoCrmv(Guid id) =>
        Ok(await _service.ObterSituacaoCrmvAsync(id));

    /// <summary>
    /// Reconsulta o conselho regional e reaplica o resultado ao perfil (RN-107).
    /// Caminho de saida de um perfil que ficou pendente por indisponibilidade do conselho.
    /// Restrito a Admins.
    /// </summary>
    [HttpPost("{id:guid}/crmv")]
    [Authorize(Policy = "ApenasAdmin")]
    [ProducesResponseType(typeof(ResultadoCrmvDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevalidarCrmv(Guid id) =>
        Ok(await _service.RevalidarCrmvAsync(id));

    /// <summary>Desativa um veterinario (soft delete — RN-022/RN-025). Restrito a Admins.</summary>
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

    // ── Agenda e servicos (RN-032/RN-034/RN-035) ─────────────────────────────

    /// <summary>Configuracao de agenda vigente do veterinario (RN-034).</summary>
    [HttpGet("{id:guid}/agenda-config")]
    [ProducesResponseType(typeof(AgendaConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterAgendaConfig(Guid id) =>
        Ok(await _agenda.ObterConfigAsync(id));

    /// <summary>
    /// Configura dias, horario, duracao e intervalo, e materializa os horarios dos
    /// proximos 60 dias (RN-034). Rematerializar nao duplica horario nem desfaz
    /// agendamento existente.
    /// </summary>
    [HttpPut("{id:guid}/agenda-config")]
    [ProducesResponseType(typeof(AgendaConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfigurarAgenda(Guid id, [FromBody] ConfigurarAgendaDto dto) =>
        Ok(await _agenda.ConfigurarAsync(id, dto));

    /// <summary>
    /// Horarios livres do veterinario no periodo, agrupados por dia — e o que o
    /// Responsavel ve ao escolher o horario (RN-034/RN-035).
    /// Sem periodo informado, devolve os proximos 14 dias.
    /// </summary>
    [HttpGet("{id:guid}/disponibilidade")]
    [ProducesResponseType(typeof(DisponibilidadeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ObterDisponibilidade(
        Guid id, [FromQuery] DateTime? de, [FromQuery] DateTime? ate) =>
        Ok(await _agenda.ObterDisponibilidadeAsync(id, de, ate));

    /// <summary>Servicos ativos do prestador, com valor e duracao (RN-032).</summary>
    [HttpGet("{id:guid}/servicos")]
    [ProducesResponseType(typeof(IEnumerable<ServicoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ObterServicos(Guid id) =>
        Ok(await _agenda.ObterServicosAsync(id));

    /// <summary>
    /// Define a vitrine de servicos do prestador (RN-032/RN-074). Servico que sai da
    /// lista e desativado, nao apagado — consulta antiga aponta para ele.
    /// </summary>
    [HttpPut("{id:guid}/servicos")]
    [ProducesResponseType(typeof(IEnumerable<ServicoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DefinirServicos(Guid id, [FromBody] DefinirServicosDto dto) =>
        Ok(await _agenda.DefinirServicosAsync(id, dto));
}
