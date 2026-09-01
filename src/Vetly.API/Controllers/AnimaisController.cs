using Vetly.Application.DTOs.Obrigacao;
using Vetly.Application.DTOs.Colmeia;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Animal;
using Vetly.Application.Interfaces;

namespace Vetly.API.Controllers;

/// <summary>Controller de animais. Gerencia CRUD, historico de prontuarios e exames.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnimaisController : ControllerBase
{
    private readonly IAnimalService _service;
    private readonly IObrigacaoService _obrigacoes;
    private readonly IColmeiaService _colmeia;

    public AnimaisController(
        IAnimalService service, IObrigacaoService obrigacoes, IColmeiaService colmeia)
    {
        _service = service;
        _obrigacoes = obrigacoes;
        _colmeia = colmeia;
    }

    /// <summary>Retorna todos os animais ativos.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AnimalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterTodos() =>
        Ok(await _service.ObterTodosAsync());

    /// <summary>Retorna um animal pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AnimalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>Board do pet: a tela inicial do Responsavel (RN-011/RN-020/RN-090).</summary>
    /// <remarks>
    /// Junta o que esta pendente, o que vem depois e o que chegou. Sao tres perguntas
    /// que o Responsavel faz ao abrir o app — "falta alguma coisa?", "quando e a
    /// proxima consulta?", "saiu algum documento?" — e uma tela que respondesse so uma
    /// delas obrigaria a navegar para descobrir o resto.
    ///
    /// <c>avatarEstado</c> e o <b>unico</b> dado do avatar que a API produz (RN-096):
    /// o sprite, a animacao e a "reclamacao" sao assets no bundle do app. Nao existe
    /// TB_AVATAR nem rota /avatar, e criar uma seria regressao de escopo.
    ///
    /// <c>alertasDeSeguranca</c> vem sempre: alergia e interacao nunca sao ocultaveis
    /// (RN-068).
    /// </remarks>
    [HttpGet("{id:guid}/board")]
    [ProducesResponseType(typeof(BoardDoPetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterBoard(Guid id) =>
        Ok(await _service.ObterBoardAsync(id));

    /// <summary>Calendario de obrigacoes do animal (RN-045/RN-046).</summary>
    /// <remarks>
    /// Mesmo board de <c>GET /api/obrigacoes/animal/{id}</c>, exposto tambem aqui
    /// porque e assim que o app do Responsavel navega: a partir do pet, e nao a partir
    /// da obrigacao.
    /// </remarks>
    [HttpGet("{id:guid}/obrigacoes")]
    [ProducesResponseType(typeof(BoardDeObrigacoesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterObrigacoes(Guid id) =>
        Ok(await _obrigacoes.ObterBoardAsync(id));

    /// <summary>Quem acessou o historico do animal, e quando (RN-067).</summary>
    /// <remarks>
    /// Visivel ao Responsavel de proposito: e a transparencia que torna a colmeia
    /// sustentavel juridicamente. Tentativa negada tambem aparece.
    /// </remarks>
    [HttpGet("{id:guid}/acessos")]
    [ProducesResponseType(typeof(IEnumerable<LogAcessoColmeiaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterAcessos(Guid id) =>
        Ok(await _colmeia.ObterLogDoAnimalAsync(id));

    /// <summary>Registra o peso aferido no atendimento (RN-081).</summary>
    /// <remarks>
    /// E a unica escrita que o veterinario faz no cadastro do animal: ele afere o peso
    /// na consulta, e sem peso a IA nao sugere dose. O resto do cadastro e do
    /// Responsavel — um vet que atendeu uma vez nao renomeia o pet nem o desativa.
    /// </remarks>
    [HttpPut("{id:guid}/peso")]
    [ProducesResponseType(typeof(AnimalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarPeso(Guid id, [FromBody] RegistrarPesoDto dto) =>
        Ok(await _service.RegistrarPesoAsync(id, dto.PesoKg));

    /// <summary>Retorna o historico longitudinal de prontuarios de um animal.</summary>
    [HttpGet("{id:guid}/prontuarios")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterProntuarios(Guid id) =>
        Ok(await _service.ObterHistoricoAsync(id));

    /// <summary>Retorna todos os exames de um animal.</summary>
    [HttpGet("{id:guid}/exames")]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterExames(Guid id) =>
        Ok(await _service.ObterExamesAsync(id));

    /// <summary>Cadastra um novo animal vinculado a um tutor.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AnimalDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarAnimalDto dto)
    {
        var criado = await _service.CriarAsync(dto);
        return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
    }

    /// <summary>Atualiza dados de um animal.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Atualizar(Guid id, [FromBody] CriarAnimalDto dto)
    {
        await _service.AtualizarAsync(id, dto);
        return NoContent();
    }

    /// <summary>Desativa um animal (soft delete).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desativar(Guid id)
    {
        await _service.DesativarAsync(id);
        return NoContent();
    }
}
