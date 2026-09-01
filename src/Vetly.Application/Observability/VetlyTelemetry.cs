using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Vetly.Application.Observability;

/// <summary>
/// Ponto unico de instrumentacao de negocio da Vetly — o <see cref="ActivitySource"/>
/// que abre os spans de dominio e o <see cref="Meter"/> que publica os contadores do
/// produto.
/// </summary>
/// <remarks>
/// <para>
/// Vive na camada de Aplicacao, e nao na API, por dois motivos. Primeiro, porque
/// <see cref="Activity"/> e <see cref="Meter"/> sao tipos da BCL (o mesmo racional do
/// <c>ILogger</c>, ja usado aqui por <c>Logging.Abstractions</c>): instrumentar nao
/// acopla a Aplicacao a OpenTelemetry, a Prometheus nem a fornecedor nenhum — quem
/// escolhe o exportador e o <c>Program.cs</c>. Segundo, porque a metrica que importa
/// para o produto nasce aqui: "consulta agendada", "pagamento confirmado", "documento
/// emitido" sao fatos de negocio, nao codigos HTTP. O ASP.NET Core ja mede latencia e
/// status; ele nao tem como saber que aquele 200 foi um agendamento perdido no checkout.
/// </para>
/// <para>
/// Instrumentos estaticos sao o padrao recomendado pelo <c>System.Diagnostics.Metrics</c>:
/// o <see cref="Meter"/> e um objeto de processo, com tempo de vida do processo, e
/// injeta-lo por DI so acrescentaria cerimonia. Nada disso e observavel ate que alguem
/// escute — sem listener registrado, <c>Add</c> e <c>Record</c> custam quase nada.
/// </para>
/// <para>
/// O <b>tracing</b> segue a mesma logica: os spans que a instrumentacao automatica do
/// ASP.NET Core produz param na fronteira do controller. Os spans abertos por
/// <see cref="Fonte"/> sao o que revela onde o tempo foi gasto <i>dentro</i> da
/// requisicao — quanto ficou no Oracle, quanto ficou esperando o LLM, quanto ficou na
/// regra. E isso que rastrear "entre camadas" significa na pratica.
/// </para>
/// </remarks>
public static class VetlyTelemetry
{
    /// <summary>
    /// Nome do servico publicado no <c>Resource</c> do OpenTelemetry — e por ele que a
    /// Vetly aparece no Jaeger, no Grafana ou em qualquer backend OTLP.
    /// </summary>
    public const string NomeDoServico = "vetly-api";

    /// <summary>
    /// Versao anunciada junto do nome do servico. Sobe junto com a release para que um
    /// grafico de latencia consiga separar "antes" e "depois" de um deploy.
    /// </summary>
    public const string Versao = "1.0.0";

    /// <summary>
    /// Nome da fonte de atividades (spans) de negocio. O <c>Program.cs</c> registra
    /// exatamente esta string em <c>AddSource</c>; span de fonte nao registrada e
    /// simplesmente descartado, entao a constante existe para que os dois lados nao
    /// possam divergir por um erro de digitacao.
    /// </summary>
    public const string NomeDaFonte = "Vetly.Application";

    /// <summary>
    /// Nome do medidor de negocio. Mesma logica da constante acima: o <c>Program.cs</c>
    /// registra este nome em <c>AddMeter</c>.
    /// </summary>
    public const string NomeDoMedidor = "Vetly.Negocio";

    /// <summary>
    /// Fonte dos spans de dominio. Use com <c>using var atividade = ...</c>: e o
    /// <c>using</c> que fecha o span e calcula a duracao.
    /// </summary>
    /// <remarks>
    /// <c>StartActivity</c> devolve <c>null</c> quando ninguem esta escutando — por isso
    /// todo consumidor usa <c>?.</c> ao anotar tags. Nao e defesa contra bug: e o
    /// caminho normal em producao com amostragem baixa.
    /// </remarks>
    public static readonly ActivitySource Fonte = new(NomeDaFonte, Versao);

    /// <summary>Medidor que agrupa todos os instrumentos de negocio declarados abaixo.</summary>
    private static readonly Meter Medidor = new(NomeDoMedidor, Versao);

    // ── Funil de agendamento (RN-003/RN-006/RN-035) ──────────────────────────
    // O produto (§10) pede para provar "conversao da busca por geolocalizacao em
    // agendamento". Isso e uma razao entre dois numeros, e os dois precisam existir:
    // quantos checkouts abriram e quantos viraram consulta confirmada.

    /// <summary>
    /// Checkouts abertos — o horario foi travado e a consulta nasceu em <c>EmCheckout</c>.
    /// Tag <c>prestador</c>: <c>clinica</c> ou <c>autonomo</c> (RN-003).
    /// </summary>
    public static readonly Counter<long> CheckoutsIniciados = Medidor.CreateCounter<long>(
        "vetly.checkouts.iniciados",
        unit: "{checkout}",
        description: "Checkouts abertos com o horario travado (RN-035).");

    /// <summary>
    /// Consultas efetivamente confirmadas pelo webhook de pagamento (RN-006).
    /// Dividida por <see cref="CheckoutsIniciados"/>, e a taxa de conversao do funil;
    /// a diferenca entre as duas e o abandono no checkout.
    /// </summary>
    public static readonly Counter<long> ConsultasConfirmadas = Medidor.CreateCounter<long>(
        "vetly.consultas.confirmadas",
        unit: "{consulta}",
        description: "Consultas confirmadas apos o pagamento (RN-006).");

    /// <summary>
    /// Cancelamentos, com tag <c>faixa</c> indicando qual Strategy respondeu
    /// (<c>integral</c>, <c>parcial</c>, <c>sem-reembolso</c>) — RN-041/RN-042.
    /// Cancelamento concentrado numa faixa e sinal de politica mal calibrada.
    /// </summary>
    public static readonly Counter<long> ConsultasCanceladas = Medidor.CreateCounter<long>(
        "vetly.consultas.canceladas",
        unit: "{consulta}",
        description: "Cancelamentos por faixa de reembolso (RN-041/RN-042).");

    // ── Dinheiro (RN-006/RN-070/RN-072) ──────────────────────────────────────

    /// <summary>
    /// Desfechos de cobranca vindos do webhook, com tag <c>status</c>
    /// (<c>confirmado</c>, <c>recusado</c>, <c>inalterado</c>) — RN-006.
    /// <c>inalterado</c> e reentrega de webhook, que e normal e precisa aparecer
    /// separada para nao poluir a taxa de recusa.
    /// </summary>
    public static readonly Counter<long> PagamentosProcessados = Medidor.CreateCounter<long>(
        "vetly.pagamentos.processados",
        unit: "{pagamento}",
        description: "Desfechos de cobranca processados pelo webhook (RN-006).");

    /// <summary>
    /// Valor bruto transacionado, em reais. E a base do split (RN-070) e o denominador
    /// de qualquer analise de take rate efetivo.
    /// </summary>
    public static readonly Histogram<double> ValorTransacionado = Medidor.CreateHistogram<double>(
        "vetly.pagamentos.valor",
        unit: "BRL",
        description: "Valor bruto por transacao confirmada (RN-070).");

    // ── Producao clinica e IA (RN-080/RN-082/RN-083) ─────────────────────────

    /// <summary>
    /// Documentos emitidos, com tag <c>tipo</c> (Prontuario, Receita, Atestado,
    /// NotaFiscal) — RN-083.
    /// </summary>
    public static readonly Counter<long> DocumentosEmitidos = Medidor.CreateCounter<long>(
        "vetly.documentos.emitidos",
        unit: "{documento}",
        description: "Documentos clinicos e fiscais emitidos (RN-083).");

    /// <summary>
    /// Decisoes do veterinario sobre o rascunho da IA, com tag <c>decisao</c>
    /// (<c>Aprovado</c>, <c>Corrigido</c>, <c>NaoAprovado</c>) — RN-082.
    /// </summary>
    /// <remarks>
    /// E a metrica que o MVP precisa provar (§10): quanto a IA reduz de fato a fricao
    /// clinica. Aprovacao sem correcao e o numerador; o total, o denominador.
    /// </remarks>
    public static readonly Counter<long> DecisoesSobreRascunho = Medidor.CreateCounter<long>(
        "vetly.ia.decisoes",
        unit: "{decisao}",
        description: "Decisoes do veterinario sobre o rascunho da IA (RN-082).");

    /// <summary>
    /// Duracao das chamadas ao LLM, em milissegundos, com tags <c>operacao</c> e
    /// <c>resultado</c>.
    /// </summary>
    /// <remarks>
    /// O Ollama e a dependencia mais lenta e mais instavel do sistema: sem esta medida,
    /// "a consulta demorou" nao se distingue de "o modelo demorou".
    /// </remarks>
    public static readonly Histogram<double> DuracaoDaIa = Medidor.CreateHistogram<double>(
        "vetly.ia.duracao",
        unit: "ms",
        description: "Tempo de resposta do LLM por operacao (§5.3).");

    // ── Guardas e integridade ────────────────────────────────────────────────

    /// <summary>
    /// Regras de negocio violadas, com tag <c>codigo</c> (RN-035, RN-060, RN-105, ...).
    /// </summary>
    /// <remarks>
    /// Este e o contador mais util do conjunto para operacao. Uma RN que dispara o tempo
    /// todo raramente e usuario mal-intencionado: quase sempre e tela que deixa o
    /// usuario tentar o que a regra proibe. O contador transforma essa suspeita em serie
    /// temporal — e um pico em RN-105 (escopo por linha), esse sim, e incidente de
    /// seguranca.
    /// </remarks>
    public static readonly Counter<long> RegrasVioladas = Medidor.CreateCounter<long>(
        "vetly.regras.violadas",
        unit: "{violacao}",
        description: "Violacoes de regra de negocio por codigo (RN-xxx).");

    // ── Worker (§11) ─────────────────────────────────────────────────────────

    /// <summary>
    /// Jobs executados pelo worker, com tags <c>tipo</c> e <c>resultado</c>
    /// (<c>sucesso</c> ou <c>falha</c>).
    /// </summary>
    /// <remarks>
    /// O worker roda fora da requisicao: sem esta metrica, uma fila que parou de drenar
    /// so aparece quando alguem reclama que o lembrete nunca chegou.
    /// </remarks>
    public static readonly Counter<long> JobsExecutados = Medidor.CreateCounter<long>(
        "vetly.jobs.executados",
        unit: "{job}",
        description: "Jobs processados pelo worker, por tipo e resultado (§11).");

    /// <summary>Duracao de cada ciclo do worker, em milissegundos.</summary>
    public static readonly Histogram<double> DuracaoDoCicloDoWorker = Medidor.CreateHistogram<double>(
        "vetly.worker.ciclo.duracao",
        unit: "ms",
        description: "Duracao do ciclo do worker de negocio (§11).");

    /// <summary>
    /// Notificacoes despachadas, com tags <c>canal</c> (<c>in-app</c>, <c>push</c>) e
    /// <c>resultado</c> — RN-092.
    /// </summary>
    public static readonly Counter<long> NotificacoesDespachadas = Medidor.CreateCounter<long>(
        "vetly.notificacoes.despachadas",
        unit: "{notificacao}",
        description: "Notificacoes entregues por canal e resultado (RN-092).");

    /// <summary>
    /// Abre um span de negocio.
    /// </summary>
    /// <param name="nome">
    /// Nome do span no formato <c>modulo.operacao</c> (ex.: <c>consulta.checkout</c>).
    /// Baixa cardinalidade e obrigatoria: id de consulta vai em tag, nunca no nome — nome
    /// de span com id gera uma serie por requisicao e inutiliza o backend.
    /// </param>
    /// <param name="tipo">
    /// Tipo do span. <see cref="ActivityKind.Internal"/> e o certo para trabalho dentro
    /// do processo; <see cref="ActivityKind.Client"/>, para chamada a dependencia externa.
    /// </param>
    /// <returns>
    /// O span aberto, ou <c>null</c> quando nao ha listener — o consumidor sempre usa
    /// <c>?.</c> ao anotar.
    /// </returns>
    public static Activity? Iniciar(string nome, ActivityKind tipo = ActivityKind.Internal) =>
        Fonte.StartActivity(nome, tipo);

    /// <summary>
    /// Marca o span como falho e anexa a excecao.
    /// </summary>
    /// <remarks>
    /// Sem isto o span sai verde: a instrumentacao automatica so enxerga o status HTTP,
    /// e uma regra violada que vira 422 tratado nao e erro de transporte nenhum. Quem
    /// investiga um trace precisa ver onde, dentro da cadeia, a operacao virou.
    /// </remarks>
    /// <param name="atividade">Span a marcar; <c>null</c> e ignorado.</param>
    /// <param name="excecao">Excecao que interrompeu a operacao.</param>
    public static void RegistrarFalha(Activity? atividade, Exception excecao)
    {
        if (atividade is null)
            return;

        atividade.SetStatus(ActivityStatusCode.Error, excecao.Message);
        atividade.AddException(excecao);
    }

    /// <summary>
    /// Marca o span <b>corrente</b> como falho — a versao usada por quem trata a excecao
    /// longe de onde o span foi aberto, como o middleware de excecao da borda HTTP.
    /// </summary>
    /// <param name="excecao">Excecao capturada.</param>
    public static void RegistrarFalhaNoSpanAtual(Exception excecao) =>
        RegistrarFalha(Activity.Current, excecao);
}
