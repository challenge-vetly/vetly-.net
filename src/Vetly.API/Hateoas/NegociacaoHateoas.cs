using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Net.Http.Headers;

namespace Vetly.API.Hateoas;

/// <summary>
/// Quando a resposta vem com hipermídia.
///
/// <para>
/// O HATEOAS é <b>opt-in</b>: sem pedido explícito, cada rota devolve exatamente o
/// corpo de antes. É o que permite ligá-lo sem quebrar o app já publicado, que lê
/// <c>{ itens, total, … }</c> e <c>{ id, status, … }</c> direto na raiz — embrulhar
/// tudo em <c>{ dados, _links }</c> por padrão mudaria o contrato de todas as rotas
/// de uma vez.
/// </para>
/// <para>
/// Duas formas de pedir: <c>Accept: application/hal+json</c>, que é a negociação de
/// conteúdo do HTTP, e <c>?hateoas=true</c>, para quem testa no navegador ou em
/// ferramenta que não deixa trocar o cabeçalho com facilidade.
/// </para>
/// </summary>
public static class NegociacaoHateoas
{
    /// <summary>Media type da resposta com hipermídia.</summary>
    public const string MediaTypeHal = "application/hal+json";

    /// <summary>Nome do parâmetro de query que liga o HATEOAS sem trocar o Accept.</summary>
    public const string ParametroDeQuery = "hateoas";

    /// <summary>A requisição pediu hipermídia, pelo Accept ou pela query.</summary>
    public static bool QuerHateoas(this HttpRequest requisicao)
    {
        if (requisicao.Query.TryGetValue(ParametroDeQuery, out var valor)
            && bool.TryParse(valor.ToString(), out var ligado))
            return ligado;

        // Parse do cabecalho, nao Contains na string: "application/hal+json;q=0" e
        // recusa explicita, e um Contains a trataria como pedido.
        var aceitos = requisicao.GetTypedHeaders().Accept;
        return aceitos.Any(a =>
            a.MediaType.Equals(MediaTypeHal, StringComparison.OrdinalIgnoreCase)
            && (a.Quality ?? 1) > 0);
    }
}

/// <summary>
/// Formatador que escreve <c>application/hal+json</c> — e só para os envelopes de
/// hipermídia.
///
/// <para>
/// Acrescentar o media type ao formatador JSON padrão seria mais curto, mas aí um
/// cliente que mandasse <c>Accept: application/hal+json</c> numa rota sem HATEOAS
/// receberia o corpo comum rotulado como hal+json — um rótulo que promete
/// <c>_links</c> que não vêm. Restringindo o tipo, essa rota cai no JSON comum e o
/// <c>Content-Type</c> diz a verdade.
/// </para>
/// </summary>
public sealed class FormatadorHalJson : SystemTextJsonOutputFormatter
{
    /// <summary>Usa as mesmas opções de serialização da API (enums como texto).</summary>
    public FormatadorHalJson(JsonSerializerOptions opcoes) : base(opcoes)
    {
        SupportedMediaTypes.Clear();
        SupportedMediaTypes.Add(MediaTypeHeaderValue.Parse(NegociacaoHateoas.MediaTypeHal));
    }

    /// <inheritdoc/>
    protected override bool CanWriteType(Type? type)
    {
        if (type is not { IsGenericType: true })
            return false;

        var definicao = type.GetGenericTypeDefinition();
        if (definicao == typeof(Recurso<>) || definicao == typeof(RecursoPaginado<>))
            return true;

        // Colecao sem paginacao: lista de Recurso<T>
        return definicao == typeof(List<>)
               && type.GetGenericArguments()[0] is { IsGenericType: true } item
               && item.GetGenericTypeDefinition() == typeof(Recurso<>);
    }
}
