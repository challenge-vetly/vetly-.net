using System.Diagnostics.Metrics;

namespace Vetly.API.Observability;

/// <summary>
/// Instrumentos de desempenho da borda HTTP: quantas requisicoes entraram, quanto
/// tempo levaram e quantas terminaram em erro.
/// </summary>
/// <remarks>
/// <para>
/// O ASP.NET Core ja publica <c>http.server.request.duration</c> pelo medidor
/// <c>Microsoft.AspNetCore.Hosting</c>, e esse medidor <b>tambem</b> e coletado (ver
/// <see cref="ConfiguracaoDeObservabilidade"/>). Este conjunto existe ao lado dele por
/// um motivo pratico: as series daqui carregam a <b>rota template</b> e a <b>classe de
/// status</b> ja resolvidas, que e a forma como se olha um painel de API na vida real
/// ("qual rota esta lenta", "a taxa de 5xx subiu"), sem precisar montar a agregacao a
/// cada consulta no Prometheus.
/// </para>
/// <para>
/// <b>Cardinalidade</b> e a unica regra que nao pode ser quebrada aqui. A tag de rota
/// usa o <i>template</i> (<c>api/consultas/{id}</c>), nunca o path concreto
/// (<c>api/consultas/9f3c...</c>): o path concreto criaria uma serie temporal nova por
/// consulta agendada, e um Prometheus com milhoes de series e um Prometheus fora do ar.
/// Pela mesma razao a tag de status e a <i>classe</i> (<c>2xx</c>, <c>4xx</c>,
/// <c>5xx</c>) alem do codigo exato.
/// </para>
/// </remarks>
public static class MetricasHttp
{
    /// <summary>
    /// Nome do medidor. Registrado em <c>AddMeter</c> no <c>Program.cs</c> — medidor nao
    /// registrado nao e coletado, e a constante impede que os dois lados divirjam.
    /// </summary>
    public const string NomeDoMedidor = "Vetly.Http";

    private static readonly Meter Medidor = new(NomeDoMedidor, "1.0.0");

    /// <summary>
    /// Total de requisicoes atendidas, com tags <c>metodo</c>, <c>rota</c>,
    /// <c>status</c> e <c>classe</c>.
    /// </summary>
    public static readonly Counter<long> Requisicoes = Medidor.CreateCounter<long>(
        "vetly.http.requisicoes",
        unit: "{requisicao}",
        description: "Requisicoes HTTP atendidas, por metodo, rota template e status.");

    /// <summary>
    /// Requisicoes que terminaram em erro (status >= 400), com as mesmas tags.
    /// </summary>
    /// <remarks>
    /// Dividido por <see cref="Requisicoes"/> no mesmo recorte, e a <b>taxa de erros</b>
    /// pedida no requisito. Mantido como contador separado — e nao derivado no painel —
    /// para que a taxa continue calculavel mesmo em backends que so recebem os
    /// contadores, sem linguagem de consulta.
    /// </remarks>
    public static readonly Counter<long> Erros = Medidor.CreateCounter<long>(
        "vetly.http.erros",
        unit: "{requisicao}",
        description: "Requisicoes HTTP com status de erro (4xx e 5xx).");

    /// <summary>
    /// Tempo de resposta, em milissegundos, com tags <c>metodo</c>, <c>rota</c> e
    /// <c>classe</c>.
    /// </summary>
    /// <remarks>
    /// Histograma, e nao media: media de latencia esconde exatamente o que interessa.
    /// Uma rota com media de 90 ms e p99 de 4 s tem um problema real que a media nunca
    /// mostra. O histograma permite ler p50, p95 e p99 no backend.
    /// </remarks>
    public static readonly Histogram<double> Duracao = Medidor.CreateHistogram<double>(
        "vetly.http.duracao",
        unit: "ms",
        description: "Tempo de resposta por rota template, em milissegundos.");

    /// <summary>
    /// Traduz o codigo HTTP na classe usada como tag de baixa cardinalidade.
    /// </summary>
    /// <param name="status">Codigo HTTP da resposta.</param>
    /// <returns><c>1xx</c> a <c>5xx</c>.</returns>
    public static string ClasseDe(int status) => status switch
    {
        >= 500 => "5xx",
        >= 400 => "4xx",
        >= 300 => "3xx",
        >= 200 => "2xx",
        _ => "1xx"
    };
}
