using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Midia;
using Vetly.Application.Interfaces;
using Vetly.Infrastructure.Adapters;

namespace Vetly.API.Controllers;

/// <summary>
/// Midias do storage de objetos (§2.6).
///
/// A API nunca proxia os bytes: ela registra a midia e entrega uma URL temporaria
/// para o app falar direto com o storage. O <c>midiaId</c> e o que viaja nos payloads
/// de negocio — nunca a URL, que expira.
/// </summary>
[ApiController]
[Route("api/midia")]
[Authorize]
public class MidiaController : ControllerBase
{
    private readonly IMidiaService _service;

    public MidiaController(IMidiaService service) => _service = service;

    /// <summary>
    /// Reserva espaco no storage e devolve a URL de upload (§2.6).
    /// </summary>
    /// <remarks>
    /// O content type e conferido aqui, e nao so no upload: aceitar qualquer coisa
    /// deixaria o storage virar deposito de arquivo arbitrario.
    /// </remarks>
    [HttpPost("upload-url")]
    [ProducesResponseType(typeof(UrlDeUploadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SolicitarUpload([FromBody] SolicitarUploadDto dto)
    {
        var url = await _service.SolicitarUploadAsync(dto);
        return Created(string.Empty, url);
    }

    /// <summary>
    /// URL temporaria para ler o arquivo. Conteudo clinico nunca vira URL publica e
    /// permanente (RN-090).
    /// </summary>
    [HttpGet("{id:guid}/url")]
    [ProducesResponseType(typeof(UrlDeLeituraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterUrlDeLeitura(Guid id) =>
        Ok(await _service.ObterUrlDeLeituraAsync(id));
}

/// <summary>
/// Recebe e serve os bytes das URLs assinadas emitidas pelo storage LOCAL.
///
/// Em producao esta rota nao existe: o app fala direto com o bucket, e e ele quem
/// valida a assinatura. Aqui ela existe porque o storage de desenvolvimento e uma
/// pasta em disco, e alguem precisa honrar a URL.
///
/// Nao exige JWT de proposito — a autorizacao e a propria assinatura da URL, que e
/// como um bucket funciona. Sem assinatura valida e dentro do prazo, nao passa.
/// </summary>
[ApiController]
[Route("api/storage")]
[AllowAnonymous]
[Filters.IsentoDeConsentimento]
public class StorageLocalController : ControllerBase
{
    private readonly IStorageAdapter _storage;
    private readonly IMidiaRepository _midias;
    private readonly IMidiaService _midiaService;

    public StorageLocalController(
        IStorageAdapter storage, IMidiaRepository midias, IMidiaService midiaService)
    {
        _storage = storage;
        _midias = midias;
        _midiaService = midiaService;
    }

    /// <summary>Recebe o arquivo de uma URL de upload assinada.</summary>
    [HttpPut("{**chave}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enviar(
        string chave,
        [FromQuery] string operacao,
        [FromQuery] long expiraEm,
        [FromQuery] string assinatura,
        CancellationToken cancellationToken)
    {
        if (_storage is not StorageAdapterLocal local)
            return NotFound();

        if (operacao != "upload" || !local.AssinaturaConfere(chave, operacao, expiraEm, assinatura))
            return Forbid();

        var midia = await _midias.ObterPorChaveAsync(chave);
        if (midia is null) return NotFound();

        await local.GravarAsync(chave, Request.Body, cancellationToken);

        var tamanho = await local.ObterTamanhoAsync(chave) ?? 0;
        await _midiaService.ConfirmarUploadAsync(midia.Id, tamanho);

        return NoContent();
    }

    /// <summary>Serve o arquivo de uma URL de leitura assinada.</summary>
    [HttpGet("{**chave}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Baixar(
        string chave,
        [FromQuery] string operacao,
        [FromQuery] long expiraEm,
        [FromQuery] string assinatura)
    {
        if (_storage is not StorageAdapterLocal local)
            return NotFound();

        if (operacao != "leitura" || !local.AssinaturaConfere(chave, operacao, expiraEm, assinatura))
            return Forbid();

        if (!await local.ExisteAsync(chave))
            return NotFound();

        var midia = await _midias.ObterPorChaveAsync(chave);

        return File(local.Abrir(chave), midia?.ContentType ?? "application/octet-stream");
    }
}
