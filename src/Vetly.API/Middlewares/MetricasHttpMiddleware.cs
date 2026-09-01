using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Vetly.API.Observability;

namespace Vetly.API.Middlewares;

/// <summary>
/// Mede cada requisicao e publica as tres series de desempenho exigidas: contagem,
/// tempo de resposta e erros (ver <see cref="MetricasHttp"/>).
/// </summary>
/// <remarks>
/// <para>
/// Posicao no pipeline importa. Este middleware fica <b>fora</b> do
/// <see cref="ExceptionHandlingMiddleware"/>, e nao dentro: por fora, ele enxerga o
/// status final que o tratador de excecao escreveu (422, 503, 500) e mede o tempo
/// incluindo o custo de montar o <c>ProblemDetails</c> — que e o tempo que o cliente
/// de fato esperou. Por dentro, mediria um caminho que termina em excecao e sairia sem
/// registrar nada justamente na requisicao que mais interessa.
/// </para>
/// <para>
/// O <c>finally</c> nao e zelo excessivo: se algo escapar de todo o pipeline, a
/// requisicao ainda entra na contagem de erro. Metrica que so registra o caminho feliz
/// mede a saude do sistema exatamente quando ele esta saudavel.
/// </para>
/// <para>
/// Sondas de saude sao ignoradas de proposito. Um orquestrador bate em
/// <c>/health/live</c> a cada poucos segundos; deixar isso entrar dominaria a contagem
/// de requisicoes e mascararia a latencia real das rotas de negocio — o painel
/// mostraria uma API sempre rapida porque a maioria das "requisicoes" nao faz nada.
/// </para>
/// </remarks>
public sealed class MetricasHttpMiddleware
{
    private readonly RequestDelegate _proximo;

    /// <summary>Encadeia o middleware seguinte do pipeline.</summary>
    /// <param name="proximo">Proximo delegate da cadeia.</param>
    public MetricasHttpMiddleware(RequestDelegate proximo) => _proximo = proximo;

    /// <summary>Cronometra a requisicao e registra os instrumentos ao fim dela.</summary>
    /// <param name="context">Contexto da requisicao HTTP em curso.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        if (EhSondaDeInfraestrutura(context.Request.Path))
        {
            await _proximo(context);
            return;
        }

        // Stopwatch com timestamp e o caminho sem alocacao — este codigo roda em toda
        // requisicao da API.
        var inicio = Stopwatch.GetTimestamp();

        try
        {
            await _proximo(context);
        }
        finally
        {
            var duracaoMs = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
            var status = context.Response.StatusCode;
            var classe = MetricasHttp.ClasseDe(status);

            // Resolvido apos o pipeline: aqui o roteamento ja aconteceu e o template
            // existe. Antes de UseRouting, o endpoint ainda e nulo.
            var rota = RotaTemplate(context);

            var tags = new TagList
            {
                { "metodo", context.Request.Method },
                { "rota", rota },
                { "classe", classe }
            };

            MetricasHttp.Duracao.Record(duracaoMs, tags);

            // O codigo exato so entra na contagem, nao no histograma: multiplicar os
            // buckets de latencia por cada status distinto explodiria a cardinalidade
            // sem responder nenhuma pergunta que a classe ja nao responda.
            var tagsComStatus = tags;
            tagsComStatus.Add("status", status);

            MetricasHttp.Requisicoes.Add(1, tagsComStatus);

            if (status >= 400)
                MetricasHttp.Erros.Add(1, tagsComStatus);
        }
    }

    /// <summary>
    /// Extrai o template da rota casada (ex.: <c>api/consultas/{id}</c>).
    /// </summary>
    /// <param name="context">Contexto ja roteado.</param>
    /// <returns>
    /// O template da rota, ou <c>(sem rota)</c> quando nenhum endpoint casou — 404 de
    /// path inexistente cai aqui, e agrupar todos eles num rotulo unico e o que impede
    /// um scanner de URLs de criar uma serie por tentativa.
    /// </returns>
    private static string RotaTemplate(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "(sem rota)";

    /// <summary>
    /// Diz se o path e uma sonda de infraestrutura (health check ou raspagem de metricas).
    /// </summary>
    /// <param name="caminho">Path da requisicao.</param>
    /// <returns><c>true</c> quando a requisicao nao deve entrar nas metricas de negocio.</returns>
    private static bool EhSondaDeInfraestrutura(PathString caminho) =>
        caminho.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
        || caminho.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase);
}
