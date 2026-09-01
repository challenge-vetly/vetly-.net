using System.Diagnostics;
using System.Diagnostics.Metrics;
using Vetly.Application.Observability;

namespace Vetly.UnitTests;

/// <summary>Uma medição capturada de um instrumento do <c>Meter</c> da Vetly.</summary>
/// <param name="Instrumento">Nome do instrumento (ex.: <c>vetly.regras.violadas</c>).</param>
/// <param name="Valor">Valor registrado, já convertido para <see cref="double"/>.</param>
/// <param name="Tags">Dimensões da medição, com os valores em texto.</param>
public sealed record Medicao(string Instrumento, double Valor, IReadOnlyDictionary<string, string?> Tags)
{
    /// <summary>Devolve o valor de uma tag, ou <c>null</c> se ela não veio.</summary>
    /// <param name="nome">Nome da tag (ex.: <c>codigo</c>, <c>resultado</c>).</param>
    public string? Tag(string nome) => Tags.TryGetValue(nome, out var valor) ? valor : null;
}

/// <summary>
/// <b>Fixture</b> que escuta a telemetria emitida pela camada de Aplicação — as
/// medições do <see cref="Meter"/> de negócio e os spans do
/// <see cref="ActivitySource"/> — e as guarda para inspeção pelos testes.
/// </summary>
/// <remarks>
/// <para>
/// Instrumentação é o caso clássico de código que "não dá para testar" e que, por isso,
/// costuma quebrar sem ninguém notar: alguém remove uma linha de <c>Add</c> num
/// refactor e a métrica simplesmente para de existir — nenhum teste falha, nenhum
/// comportamento muda, e o painel só some meses depois, quando alguém for olhar. O
/// <see cref="MeterListener"/> e o <see cref="ActivityListener"/> são a API que a BCL
/// oferece justamente para fechar essa lacuna: são os mesmos mecanismos que o
/// OpenTelemetry usa por baixo, só que apontados para uma lista em memória.
/// </para>
/// <para>
/// <b>Por que Collection Fixture e não Class Fixture:</b> um listener é uma inscrição
/// de processo. Vários listeners simultâneos sobre os mesmos instrumentos estáticos
/// funcionam, mas duplicam trabalho e tornam a ordem de callback imprevisível. Uma
/// única instância compartilhada pela coleção é o modelo correto — e o custo de montá-la
/// (percorrer os instrumentos publicados) é pago uma vez, não uma por classe.
/// </para>
/// <para>
/// <b>Consequência para quem escreve os testes:</b> o xUnit executa outras coleções em
/// paralelo, e classes de teste de negócio também exercitam os serviços instrumentados.
/// Isso significa que este coletor pode receber medições de terceiros. Por isso as
/// asserções aqui são sempre do tipo "contém uma medição com estas tags", nunca
/// "recebeu exatamente uma medição" — e os testes usam valores-sentinela próprios,
/// impossíveis de colidir com o restante da suíte.
/// </para>
/// </remarks>
public sealed class ColetorDeTelemetriaFixture : IDisposable
{
    private readonly MeterListener _ouvinteDeMetricas;
    private readonly ActivityListener _ouvinteDeSpans;

    private readonly List<Medicao> _medicoes = [];
    private readonly List<Activity> _spans = [];

    // Os callbacks chegam da thread que emitiu a medição. O lock é o que impede uma
    // colisão de escrita quando duas classes de teste rodam em paralelo.
    private readonly Lock _trava = new();

    /// <summary>Monta e inicia os dois ouvintes.</summary>
    public ColetorDeTelemetriaFixture()
    {
        _ouvinteDeMetricas = new MeterListener
        {
            // Chamado para cada instrumento publicado no processo. Filtrar pelo nome do
            // medidor evita capturar as métricas da plataforma (Kestrel, runtime).
            InstrumentPublished = (instrumento, ouvinte) =>
            {
                if (instrumento.Meter.Name == VetlyTelemetry.NomeDoMedidor)
                    ouvinte.EnableMeasurementEvents(instrumento);
            }
        };

        // Um callback por tipo numérico: contadores usam long, histogramas usam double.
        _ouvinteDeMetricas.SetMeasurementEventCallback<long>(
            (instrumento, valor, tags, _) => Registrar(instrumento.Name, valor, tags));

        _ouvinteDeMetricas.SetMeasurementEventCallback<double>(
            (instrumento, valor, tags, _) => Registrar(instrumento.Name, valor, tags));

        _ouvinteDeMetricas.Start();

        _ouvinteDeSpans = new ActivityListener
        {
            ShouldListenTo = fonte => fonte.Name == VetlyTelemetry.NomeDaFonte,

            // Sem amostragem "AllDataAndRecorded", StartActivity devolve null e o span
            // sequer é criado — é exatamente o que acontece em produção quando ninguém
            // está escutando, e o que tornaria estes testes inúteis.
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,

            // No Stopped, e não no Started: só ao fechar é que o span tem duração e as
            // tags anotadas ao longo da operação.
            ActivityStopped = atividade =>
            {
                lock (_trava)
                    _spans.Add(atividade);
            }
        };

        ActivitySource.AddActivityListener(_ouvinteDeSpans);
    }

    /// <summary>Todas as medições capturadas até agora.</summary>
    public IReadOnlyList<Medicao> Medicoes
    {
        get { lock (_trava) return [.. _medicoes]; }
    }

    /// <summary>Todos os spans encerrados até agora.</summary>
    public IReadOnlyList<Activity> Spans
    {
        get { lock (_trava) return [.. _spans]; }
    }

    /// <summary>Medições de um instrumento específico.</summary>
    /// <param name="instrumento">Nome do instrumento (ex.: <c>vetly.ia.duracao</c>).</param>
    public IReadOnlyList<Medicao> De(string instrumento) =>
        [.. Medicoes.Where(m => m.Instrumento == instrumento)];

    /// <summary>Spans com um nome específico.</summary>
    /// <param name="nome">Nome do span (ex.: <c>consulta.checkout</c>).</param>
    public IReadOnlyList<Activity> SpansChamados(string nome) =>
        [.. Spans.Where(s => s.OperationName == nome)];

    /// <summary>
    /// Descarta o que foi capturado. Chamado no Arrange dos testes para que a asserção
    /// olhe só para o que o Act produziu.
    /// </summary>
    public void Limpar()
    {
        lock (_trava)
        {
            _medicoes.Clear();
            _spans.Clear();
        }
    }

    /// <summary>
    /// Converte a medição para um registro imutável.
    /// </summary>
    /// <remarks>
    /// As tags chegam como <see cref="ReadOnlySpan{T}"/>, que não pode ser capturado por
    /// closure nem guardado em campo — a cópia para dicionário aqui não é preferência de
    /// estilo, é o que a linguagem exige.
    /// </remarks>
    private void Registrar(string instrumento, double valor, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var copia = new Dictionary<string, string?>(tags.Length);

        foreach (var tag in tags)
            copia[tag.Key] = tag.Value?.ToString();

        lock (_trava)
            _medicoes.Add(new Medicao(instrumento, valor, copia));
    }

    /// <summary>Encerra os dois ouvintes ao fim da coleção.</summary>
    public void Dispose()
    {
        _ouvinteDeMetricas.Dispose();
        _ouvinteDeSpans.Dispose();
    }
}

/// <summary>
/// Declara a coleção que compartilha o <see cref="ColetorDeTelemetriaFixture"/>.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class ColecaoDeTelemetria : ICollectionFixture<ColetorDeTelemetriaFixture>
{
    /// <summary>Nome da coleção, usado em <c>[Collection(...)]</c>.</summary>
    public const string Nome = "Telemetria (coletor compartilhado)";
}
