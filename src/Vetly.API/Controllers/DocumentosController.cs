using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Documento;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.API.Controllers;

/// <summary>
/// Controller de documentos clinicos.
/// Geracao requer diagnostico validado e parte do estado final aprovado (RN-082/RN-083).
/// Assinatura digital (RN-087) e correcao de versao (RN-088/RN-089).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DocumentosController : ControllerBase
{
    private readonly IDocumentoService _service;

    public DocumentosController(IDocumentoService service) => _service = service;

    /// <summary>Retorna todos os documentos de uma consulta.</summary>
    [HttpGet("consulta/{consultaId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<DocumentoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterPorConsulta(Guid consultaId) =>
        Ok(await _service.ObterPorConsultaAsync(consultaId));

    /// <summary>Retorna um documento pelo ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId(Guid id) =>
        Ok(await _service.ObterPorIdAsync(id));

    /// <summary>
    /// Gera um documento para uma consulta, selecionando a Factory pelo tipo
    /// (RN-082/RN-083).
    /// </summary>
    /// <remarks>
    /// O conteudo e formatado a partir do estado final aprovado pelo veterinario, lido
    /// da trilha de auditoria — nao do rascunho da IA. Gerar documento e formatar o que
    /// ja foi decidido; se aqui houvesse nova inferencia, o que fosse impresso poderia
    /// divergir do que o profissional aprovou.
    ///
    /// Sem conteudo aprovado devolve 422: decida sobre o rascunho ou registre o
    /// prontuario manual antes.
    ///
    /// O PDF renderizado e anexado no mesmo passo (RN-090); <c>pdfMidiaId</c> na
    /// resposta e o que se usa para pedir a URL temporaria de leitura.
    ///
    /// <c>subtipo</c> so vale para o Atestado, e muda o texto do documento, nao apenas
    /// o rotulo (RN-086).
    /// </remarks>
    [HttpPost("consulta/{consultaId:guid}")]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Gerar(
        Guid consultaId, [FromQuery] TipoDocumento tipo, [FromQuery] TipoAtestado? subtipo = null)
    {
        var doc = await _service.GerarAsync(consultaId, tipo, subtipo);

        return CreatedAtAction(nameof(ObterPorId), new { id = doc.Id }, doc);
    }

    /// <summary>Assina o documento pelo adaptador de assinatura (RN-087).</summary>
    /// <remarks>
    /// Somente o veterinario que conduziu o atendimento assina seus documentos; fora
    /// do escopo devolve 403 (RN-105). Documento ja assinado devolve 409.
    ///
    /// No MVP a assinatura e o nome digitado, conferido contra o nome registrado. O
    /// carimbo entra no corpo do documento e diz como ele foi assinado — inclusive que
    /// nao habilita dispensacao de controlado fora da plataforma. Omitir isso seria
    /// deixar o documento parecer mais do que e.
    /// </remarks>
    [HttpPost("{id:guid}/assinar")]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Assinar(Guid id, [FromBody] AssinaturaRequest request) =>
        Ok(await _service.AssinarAsync(id, request.NomeCompleto));

    /// <summary>Cria uma versao corrigida de um documento (RN-088/RN-089).</summary>
    [HttpPost("{id:guid}/correcao")]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Corrigir(Guid id, [FromBody] CorrecaoDocumentoRequest request) =>
        Ok(await _service.CorrigirAsync(id, request.NovosDados, request.Justificativa, request.CrmvSolicitante));

    /// <summary>
    /// Publica o documento no board do pet, onde o Responsavel o alcanca
    /// (RN-011/RN-090).
    /// </summary>
    /// <remarks>
    /// Gerar e publicar sao passos separados de proposito: o veterinario pode gerar,
    /// conferir e so entao entregar. Receita sem assinatura nao e publicada — no board
    /// ela pareceria valida sem ser (RN-087).
    ///
    /// Idempotente: republicar preserva a data original, que e a referencia da
    /// notificacao ao Responsavel.
    /// </remarks>
    [HttpPost("{id:guid}/publicar")]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Publicar(Guid id) =>
        Ok(await _service.PublicarAsync(id));

    /// <summary>Registra que o Responsavel abriu o documento no app.</summary>
    /// <remarks>
    /// So vale para documento ja publicado, e a primeira leitura e a que fica: e o
    /// dado que diz se a orientacao chegou de fato a quem cuida do animal.
    /// </remarks>
    [HttpPost("{id:guid}/lido")]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MarcarComoLido(Guid id) =>
        Ok(await _service.MarcarComoLidoAsync(id));

    /// <summary>Board do pet: documentos publicados de um animal (RN-011/RN-090).</summary>
    /// <remarks>
    /// Documento gerado mas ainda nao publicado nao aparece aqui — o Responsavel nao
    /// deve ver rascunho de documento.
    ///
    /// O escopo vem do token: o Responsavel alcanca so os proprios animais, e o
    /// veterinario so os que atende (RN-105/RN-106).
    /// </remarks>
    [HttpGet("animal/{animalId:guid}")]
    [ProducesResponseType(typeof(IEnumerable<DocumentoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterBoardDoPet(Guid animalId) =>
        Ok(await _service.ObterDoBoardDoPetAsync(animalId));
}

/// <summary>Payload da assinatura (RN-087).</summary>
public sealed class AssinaturaRequest
{
    /// <summary>
    /// Nome completo do veterinario, digitado no ato de assinar. E conferido contra o
    /// nome registrado — assinar em nome de outro profissional e o que a conferencia
    /// existe para impedir.
    /// </summary>
    public string? NomeCompleto { get; set; }
}

/// <summary>Payload para correcao de documento.</summary>
public sealed class CorrecaoDocumentoRequest
{
    public string NovosDados { get; set; } = string.Empty;
    public string? Justificativa { get; set; }
    public string CrmvSolicitante { get; set; } = string.Empty;
}
