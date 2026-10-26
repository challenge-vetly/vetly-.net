using Vetly.API.Controllers;
using Vetly.Application.DTOs.Documento;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.API.Hateoas;

/// <summary>
/// Links de um documento clínico pelo ciclo assinatura → publicação → leitura
/// (RN-087/RN-090).
///
/// <list type="bullet">
/// <item>Veterinário e Admin: <c>assinar</c> enquanto não assinado; <c>publicar</c>
/// enquanto não publicado — e a receita só depois de assinada (RN-087);
/// <c>corrigir</c> a qualquer tempo (RN-088).</item>
/// <item>Responsável: <c>marcar-lido</c> quando publicado e ainda não lido.</item>
/// <item>Todos: o PDF, quando já renderizado, e a consulta de origem.</item>
/// </list>
/// </summary>
public sealed class LinksDeDocumento : ILinksDoRecurso<DocumentoDto>
{
    private readonly ConstrutorDeLinks _links;
    private readonly IUsuarioAtual _usuario;

    /// <summary>Cria o fornecedor com o perfil do token da requisição.</summary>
    public LinksDeDocumento(ConstrutorDeLinks links, IUsuarioAtual usuario)
    {
        _links = links;
        _usuario = usuario;
    }

    /// <inheritdoc/>
    public IEnumerable<Link> Para(DocumentoDto documento)
    {
        var id = new { id = documento.Id };

        yield return _links.Para<DocumentosController>("self", HttpMethods.Get, nameof(DocumentosController.ObterPorId), id);

        if (documento.PdfMidiaId is { } pdf)
            yield return _links.Para<MidiaController>(
                "pdf", HttpMethods.Get, nameof(MidiaController.ObterUrlDeLeitura), new { id = pdf });

        if (documento.ConsultaId is { } consultaId)
            yield return _links.Para<ConsultasController>(
                "consulta", HttpMethods.Get, nameof(ConsultasController.ObterPorId), new { id = consultaId });

        if (_usuario.EhVeterinario || _usuario.EhAdmin)
        {
            if (!documento.AssinadoDigitalmente)
                yield return _links.Para<DocumentosController>(
                    "assinar", HttpMethods.Post, nameof(DocumentosController.Assinar), id);

            var podePublicar = documento.PublicadoEm is null
                && (documento.TipoDocumento != TipoDocumento.ReceitaVeterinaria || documento.AssinadoDigitalmente);

            if (podePublicar)
                yield return _links.Para<DocumentosController>(
                    "publicar", HttpMethods.Post, nameof(DocumentosController.Publicar), id);

            yield return _links.Para<DocumentosController>(
                "corrigir", HttpMethods.Post, nameof(DocumentosController.Corrigir), id);
        }

        if (_usuario.EhTutor && documento.PublicadoEm is not null && documento.LidoEm is null)
            yield return _links.Para<DocumentosController>(
                "marcar-lido", HttpMethods.Post, nameof(DocumentosController.MarcarComoLido), id);
    }
}
