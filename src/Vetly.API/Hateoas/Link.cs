using System.Text.Json.Serialization;

namespace Vetly.API.Hateoas;

/// <summary>
/// Um link de hipermídia: para onde ir, por que e com qual verbo.
///
/// <para>
/// O <see cref="Metodo"/> vai junto porque o mesmo endereço significa coisas
/// diferentes conforme o verbo — <c>/api/consultas/{id}</c> é "ler" com GET e
/// "cancelar" com DELETE. Um link sem verbo obrigaria o app a manter uma tabela
/// própria de "este rel usa este método", que é justamente o acoplamento que o
/// HATEOAS existe para tirar do cliente.
/// </para>
/// </summary>
/// <param name="Href">Caminho relativo à raiz da API (ex.: <c>/api/consultas/…</c>).</param>
/// <param name="Rel">Relação do link com o recurso (ex.: <c>self</c>, <c>cancelar</c>).</param>
/// <param name="Metodo">Verbo HTTP em maiúsculas.</param>
public sealed record Link(
    [property: JsonPropertyName("href")] string Href,
    [property: JsonPropertyName("rel")] string Rel,
    [property: JsonPropertyName("metodo")] string Metodo);

/// <summary>
/// Um recurso com os links do que dá para fazer com ele <b>agora</b>.
///
/// Os links são calculados a partir do estado do recurso e do perfil de quem pede:
/// uma consulta cancelada não oferece "remarcar", e um link que a policy vai recusar
/// ao usuário do token não aparece. Oferecer ação impossível é pior que não oferecer
/// nada — o app mostra o botão e o Responsável recebe 403.
/// </summary>
/// <typeparam name="T">O DTO devolvido pela rota sem HATEOAS, inalterado.</typeparam>
public sealed class Recurso<T>
{
    /// <summary>O mesmo corpo que a rota devolve sem HATEOAS.</summary>
    [JsonPropertyName("dados")]
    public T Dados { get; init; } = default!;

    /// <summary>Ações e navegações disponíveis para este recurso.</summary>
    [JsonPropertyName("_links")]
    public IReadOnlyList<Link> Links { get; init; } = [];
}

/// <summary>
/// Envelope de uma listagem paginada com HATEOAS: os mesmos metadados do
/// <c>ResultadoPaginado</c>, cada item com os próprios links e a navegação entre
/// páginas.
///
/// Os links de página preservam filtros e ordenação da requisição original. Sem isso,
/// "próxima página" de uma busca filtrada voltaria a listar tudo — e o cliente teria
/// de remontar a query string, que é o que os links existem para evitar.
/// </summary>
/// <typeparam name="T">Tipo do item da página.</typeparam>
public sealed class RecursoPaginado<T>
{
    /// <summary>Itens da página, cada um com os próprios links.</summary>
    [JsonPropertyName("itens")]
    public IReadOnlyList<Recurso<T>> Itens { get; init; } = [];

    /// <summary>Total de registros que atendem ao filtro, ignorando a paginação.</summary>
    [JsonPropertyName("total")]
    public int Total { get; init; }

    /// <summary>Página retornada, começando em 1.</summary>
    [JsonPropertyName("pagina")]
    public int Pagina { get; init; }

    /// <summary>Itens por página efetivamente aplicados.</summary>
    [JsonPropertyName("tamanho")]
    public int Tamanho { get; init; }

    /// <summary>Total de páginas para o filtro atual.</summary>
    [JsonPropertyName("totalDePaginas")]
    public int TotalDePaginas { get; init; }

    /// <summary>
    /// Navegação: <c>self</c>, <c>primeira</c>, <c>anterior</c>, <c>proxima</c> e
    /// <c>ultima</c>. <c>anterior</c> e <c>proxima</c> só aparecem quando existem.
    /// </summary>
    [JsonPropertyName("_links")]
    public IReadOnlyList<Link> Links { get; init; } = [];
}
