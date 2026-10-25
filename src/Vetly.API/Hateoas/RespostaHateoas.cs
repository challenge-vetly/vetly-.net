using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Comum;

namespace Vetly.API.Hateoas;

/// <summary>
/// Links de um tipo de recurso, calculados pelo estado do recurso e pelo perfil do
/// token. Cada DTO que ganha HATEOAS tem uma implementação registrada no contêiner.
/// </summary>
/// <typeparam name="T">DTO devolvido pela rota.</typeparam>
public interface ILinksDoRecurso<in T>
{
    /// <summary>Links do que dá para fazer com o recurso agora, por quem pediu.</summary>
    IEnumerable<Link> Para(T recurso);
}

/// <summary>
/// Monta a resposta das rotas com HATEOAS: o corpo de sempre, ou o envelope com links
/// quando a requisição pede hipermídia (ver <see cref="NegociacaoHateoas"/>).
///
/// Fica na camada de API de propósito. Link é detalhe de transporte HTTP: o serviço
/// devolve o DTO, e só aqui se sabe que existe rota, verbo e media type. Se o
/// HATEOAS morasse no Application, cada serviço dependeria do roteamento do ASP.NET.
/// </summary>
public sealed class RespostaHateoas
{
    private readonly IHttpContextAccessor _acessor;
    private readonly ConstrutorDeLinks _links;
    private readonly IServiceProvider _servicos;

    /// <summary>Cria a fábrica de respostas da requisição.</summary>
    public RespostaHateoas(IHttpContextAccessor acessor, ConstrutorDeLinks links, IServiceProvider servicos)
    {
        _acessor = acessor;
        _links = links;
        _servicos = servicos;
    }

    private bool QuerHateoas => _acessor.HttpContext?.Request.QuerHateoas() == true;

    /// <summary>
    /// 200 com o recurso, embrulhado com os links quando a requisição pede.
    /// </summary>
    public IActionResult Ok<T>(T recurso) =>
        QuerHateoas ? Hal(Envolver(recurso)) : new OkObjectResult(recurso);

    /// <summary>
    /// 200 com a página, embrulhada com os links de cada item e de navegação quando a
    /// requisição pede.
    /// </summary>
    public IActionResult Ok<T>(ResultadoPaginado<T> pagina)
    {
        if (!QuerHateoas)
            return new OkObjectResult(pagina);

        return Hal(new RecursoPaginado<T>
        {
            Itens = pagina.Itens.Select(Envolver).ToList(),
            Total = pagina.Total,
            Pagina = pagina.Pagina,
            Tamanho = pagina.Tamanho,
            TotalDePaginas = pagina.TotalDePaginas,
            Links = _links.DePaginacao(pagina)
        });
    }

    /// <summary>
    /// 200 com uma coleção sem paginação (listas curtas por natureza, como os
    /// documentos de uma consulta). Com HATEOAS, cada item vem com os próprios links.
    /// </summary>
    public IActionResult OkColecao<T>(IEnumerable<T> itens)
    {
        if (!QuerHateoas)
            return new OkObjectResult(itens);

        return Hal(itens.Select(Envolver).ToList());
    }

    private Recurso<T> Envolver<T>(T recurso)
    {
        // Resolucao pelo tipo fechado, como ILogger<T>. Faltar o registro e erro de
        // montagem, nao de requisicao: falha alto em vez de devolver o recurso sem links.
        var fornecedor = _servicos.GetService(typeof(ILinksDoRecurso<T>)) as ILinksDoRecurso<T>
            ?? throw new InvalidOperationException(
                $"Nenhum ILinksDoRecurso<{typeof(T).Name}> registrado.");

        return new Recurso<T> { Dados = recurso, Links = fornecedor.Para(recurso).ToList() };
    }

    private static ObjectResult Hal(object corpo) => new(corpo)
    {
        StatusCode = StatusCodes.Status200OK,
        ContentTypes = { NegociacaoHateoas.MediaTypeHal }
    };
}
