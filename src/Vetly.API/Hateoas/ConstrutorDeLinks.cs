using Microsoft.AspNetCore.Mvc;
using Vetly.Application.DTOs.Comum;

namespace Vetly.API.Hateoas;

/// <summary>
/// Monta os links a partir das rotas registradas.
///
/// <para>
/// Todo endereço sai do <see cref="LinkGenerator"/>, pelo nome da action — nunca de
/// concatenação de texto. Concatenar repete o template da rota num segundo lugar, e
/// no dia em que a rota mudar os links continuam apontando para o endereço velho sem
/// nenhum teste quebrar. Pelo nome da action, uma rota renomeada leva os links junto,
/// e uma action removida faz o link virar nulo — o que o teste de HATEOAS pega.
/// </para>
/// <para>
/// Os endereços são caminhos (<c>/api/…</c>), não URLs absolutas: atrás do Caddy o
/// host e o esquema que a API enxerga dependem dos cabeçalhos de proxy, e um caminho
/// relativo vale igual no deploy, no ambiente local e nos testes.
/// </para>
/// </summary>
public sealed class ConstrutorDeLinks
{
    private readonly LinkGenerator _gerador;
    private readonly IHttpContextAccessor _acessor;

    /// <summary>Cria o construtor sobre o gerador de rotas da aplicação.</summary>
    public ConstrutorDeLinks(LinkGenerator gerador, IHttpContextAccessor acessor)
    {
        _gerador = gerador;
        _acessor = acessor;
    }

    private HttpContext Contexto =>
        _acessor.HttpContext ?? throw new InvalidOperationException(
            "Links de HATEOAS so podem ser montados durante uma requisicao.");

    /// <summary>
    /// Link para uma action de um controller.
    /// </summary>
    /// <param name="rel">Relação do link com o recurso.</param>
    /// <param name="metodo">Verbo HTTP.</param>
    /// <param name="acao">Nome da action (use <c>nameof</c>).</param>
    /// <param name="controller">Nome do controller sem o sufixo <c>Controller</c>.</param>
    /// <param name="valores">Valores de rota e de query.</param>
    /// <exception cref="InvalidOperationException">
    /// A action não existe ou os valores não satisfazem a rota. Falha alto de
    /// propósito: um link quebrado silencioso é pior que nenhum link.
    /// </exception>
    public Link Para(string rel, string metodo, string acao, string controller, object? valores = null)
    {
        var caminho = _gerador.GetPathByAction(Contexto, acao, controller, valores)
            ?? throw new InvalidOperationException(
                $"Nao ha rota para {controller}.{acao} com os valores informados.");

        return new Link(caminho, rel, metodo);
    }

    /// <summary>
    /// Link para uma action de <typeparamref name="TController"/>. O nome do
    /// controller sai do tipo, e um controller renomeado não deixa link órfão.
    /// </summary>
    public Link Para<TController>(string rel, string metodo, string acao, object? valores = null)
        where TController : ControllerBase
    {
        const string sufixo = "Controller";
        var nome = typeof(TController).Name;
        var controller = nome.EndsWith(sufixo, StringComparison.Ordinal) ? nome[..^sufixo.Length] : nome;

        return Para(rel, metodo, acao, controller, valores);
    }

    /// <summary>
    /// Links de navegação de uma listagem paginada: <c>self</c>, <c>primeira</c>,
    /// <c>anterior</c>, <c>proxima</c> e <c>ultima</c>.
    ///
    /// Partem da rota da requisição atual e copiam a query string inteira, trocando
    /// só a página. Assim filtros, ordenação, tamanho e o próprio <c>hateoas=true</c>
    /// atravessam a navegação sem o cliente remontar nada.
    /// </summary>
    public IReadOnlyList<Link> DePaginacao<T>(ResultadoPaginado<T> pagina)
    {
        var links = new List<Link> { ParaPagina("self", pagina.Pagina, pagina.Tamanho) };

        // Lista vazia ainda tem uma "primeira" pagina; "ultima" nunca e menor que ela.
        var ultima = Math.Max(1, pagina.TotalDePaginas);

        links.Add(ParaPagina("primeira", 1, pagina.Tamanho));

        if (pagina.Pagina > 1)
            links.Add(ParaPagina("anterior", Math.Min(pagina.Pagina - 1, ultima), pagina.Tamanho));

        if (pagina.TemProximaPagina)
            links.Add(ParaPagina("proxima", pagina.Pagina + 1, pagina.Tamanho));

        links.Add(ParaPagina("ultima", ultima, pagina.Tamanho));

        return links;
    }

    private Link ParaPagina(string rel, int numero, int tamanho)
    {
        var contexto = Contexto;

        // Valores de rota da requisicao (ex.: o {id} de /tutores/{id}/carteira/historico)
        // mais a query inteira; o gerador poe na query tudo que nao e parametro de rota.
        var valores = new RouteValueDictionary(contexto.Request.RouteValues);
        foreach (var (chave, valor) in contexto.Request.Query)
            valores[chave] = valor.ToString();

        valores[nameof(Paginacao.Pagina).ToLowerInvariant()] = numero;
        valores[nameof(Paginacao.Tamanho).ToLowerInvariant()] = tamanho;

        // Action e controller ficam nulos: valem os da requisicao atual (valores ambientes).
        var caminho = _gerador.GetPathByAction(contexto, action: null, controller: null, values: valores)
            ?? throw new InvalidOperationException("Nao foi possivel montar o link de paginacao.");

        return new Link(caminho, rel, HttpMethods.Get);
    }
}
