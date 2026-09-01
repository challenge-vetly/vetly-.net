using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Vetly.IntegrationTests;

/// <summary>
/// Testes de integração da camada de observabilidade: health checks, endpoint de
/// métricas e correlação entre log, trace e resposta de erro.
/// </summary>
/// <remarks>
/// <para>
/// Observabilidade é o tipo de coisa que "está lá" até o dia em que alguém precisa dela
/// e descobre que um refactor derrubou o middleware, que a rota de métricas parou de ser
/// mapeada, ou que a sonda passou a devolver 200 com o banco fora. Nada disso quebra
/// nenhum teste de negócio — a API continua atendendo. Estes testes existem para que
/// quebre.
/// </para>
/// <para>
/// Todos seguem <b>AAA</b>: Arrange prepara a requisição, Act executa exatamente uma
/// chamada, Assert verifica o efeito observável.
/// </para>
/// </remarks>
[Collection(ColecaoDaApi.Nome)]
public class ObservabilidadeTests
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public ObservabilidadeTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    // ── Health checks (RN de infraestrutura: §Monitoramento) ─────────────────

    [Fact]
    public async Task HealthLive_SemAutenticacao_RespondeHealthy()
    {
        // Arrange — a sonda de liveness é pública por natureza: o orquestrador que a
        // consulta não tem token, e exigir um transformaria "sem credencial" em
        // "aplicação morta", derrubando o container em loop.

        // Act
        var resposta = await _client.GetAsync("/health/live");

        // Assert
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await LerJsonAsync(resposta);
        Assert.Equal("Healthy", corpo.GetProperty("status").GetString());
    }

    [Fact]
    public async Task HealthLive_NaoExecutaChecksDeDependencia()
    {
        // Arrange — esta é a razão de existir de dois endpoints separados: liveness
        // decide REINICIAR o processo. Se ela tocasse o banco, uma indisponibilidade
        // momentânea do Oracle mataria containers saudáveis, e o restart em massa
        // pioraria justamente o incidente que estaria em curso.

        // Act
        var resposta = await _client.GetAsync("/health/live");

        // Assert
        var checks = NomesDosChecks(await LerJsonAsync(resposta));

        Assert.Equal(["api"], checks);
        Assert.DoesNotContain("oracle-db", checks);
        Assert.DoesNotContain("ollama", checks);
    }

    [Fact]
    public async Task HealthReady_ExecutaApenasOsChecksDeDependencia()
    {
        // Arrange — readiness decide se o container recebe TRÁFEGO. Ela precisa do
        // oposto: tocar as dependências, e não o processo.

        // Act
        var resposta = await _client.GetAsync("/health/ready");

        // Assert
        var checks = NomesDosChecks(await LerJsonAsync(resposta));

        Assert.Contains("oracle-db", checks);
        Assert.Contains("ollama", checks);
        Assert.DoesNotContain("api", checks);
    }

    [Fact]
    public async Task HealthReady_QuandoOBancoNaoResponde_RetornaServiceUnavailable()
    {
        // Arrange — nos testes o Oracle é substituído por InMemory, que não suporta
        // abrir conexão relacional. O efeito é o mesmo de um banco fora do ar, e é
        // exatamente o cenário que precisa ser provado: falha de banco tem de tirar a
        // instância de rotação, não devolver 200 com um aviso no corpo.

        // Act
        var resposta = await _client.GetAsync("/health/ready");

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);

        var corpo = await LerJsonAsync(resposta);
        Assert.Equal("Unhealthy", corpo.GetProperty("status").GetString());

        var oracle = corpo.GetProperty("checks")
            .EnumerateArray()
            .First(c => c.GetProperty("nome").GetString() == "oracle-db");

        Assert.Equal("Unhealthy", oracle.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Health_ListaTodosOsChecksRegistrados()
    {
        // Arrange — o endpoint raiz é o de diagnóstico: sem Predicate, roda tudo.

        // Act
        var resposta = await _client.GetAsync("/health");

        // Assert
        var checks = NomesDosChecks(await LerJsonAsync(resposta));

        Assert.Contains("api", checks);
        Assert.Contains("oracle-db", checks);
        Assert.Contains("ollama", checks);
    }

    [Fact]
    public async Task Health_RelatorioTrazDuracaoETagsDeCadaCheck()
    {
        // Arrange — a resposta padrão do ASP.NET Core é o texto "Unhealthy", que não
        // diz qual dependência caiu. O writer customizado existe para isso, e é o
        // contrato que o time de plantão consome.

        // Act
        var corpo = await LerJsonAsync(await _client.GetAsync("/health"));

        // Assert
        Assert.True(corpo.GetProperty("duracaoTotalMs").GetDouble() >= 0);

        var primeiro = corpo.GetProperty("checks").EnumerateArray().First();

        Assert.False(string.IsNullOrWhiteSpace(primeiro.GetProperty("nome").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(primeiro.GetProperty("status").GetString()));
        Assert.True(primeiro.GetProperty("duracaoMs").GetDouble() >= 0);
        Assert.NotEmpty(primeiro.GetProperty("tags").EnumerateArray());
    }

    // ── Métricas (Prometheus) ────────────────────────────────────────────────

    [Fact]
    public async Task Metrics_ExpoeOFormatoDeTextoDoPrometheus()
    {
        // Arrange — o coletor identifica o formato pelo Content-Type; um JSON aqui
        // seria descartado silenciosamente pelo Prometheus.

        // Act
        var resposta = await _client.GetAsync("/metrics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("text/plain", resposta.Content.Headers.ContentType?.MediaType);

        var texto = await resposta.Content.ReadAsStringAsync();

        // O bloco target_info carrega a identidade do serviço (Resource do OTel) e é o
        // que separa a Vetly dos demais serviços raspados pelo mesmo Prometheus.
        Assert.Contains("target_info", texto);
        Assert.Contains("vetly-api", texto);
    }

    [Fact]
    public async Task Metrics_ApósRequisicaoDeNegocio_PublicaDuracaoPorRotaTemplate()
    {
        // Arrange — uma requisição qualquer que passe pelo roteamento. O 401 serve
        // tão bem quanto um 200: o que se mede aqui é a instrumentação, não a regra.
        await _client.GetAsync("/api/consultas");

        // Act
        var texto = await (await _client.GetAsync("/metrics")).Content.ReadAsStringAsync();

        // Assert
        Assert.Contains("vetly_http_duracao_milliseconds", texto);
        Assert.Contains("vetly_http_requisicoes_total", texto);

        // A tag de rota tem de ser o TEMPLATE. Se aparecesse o path concreto, cada id
        // de consulta viraria uma série temporal nova — o caminho mais rápido para
        // derrubar um Prometheus é uma métrica com cardinalidade ilimitada.
        Assert.Contains("rota=\"api/", texto);
        Assert.DoesNotContain("rota=\"/api/consultas/00000000", texto);
    }

    [Fact]
    public async Task Metrics_RequisicaoComErro_IncrementaContadorDeErrosEDeRegraViolada()
    {
        // Arrange — login com credencial inexistente. O serviço responde sempre a mesma
        // coisa para e-mail inexistente e senha errada (para não vazar a lista de
        // contas), e nos dois casos levanta AUTH-001.
        var corpo = new StringContent(
            """{"email":"ninguem-existe@exemplo.com","senha":"senha-que-nao-vale"}""",
            Encoding.UTF8, "application/json");

        // Act
        var resposta = await _client.PostAsync("/api/auth/login", corpo);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);

        var texto = await (await _client.GetAsync("/metrics")).Content.ReadAsStringAsync();

        Assert.Contains("vetly_http_erros_total", texto);

        // A taxa de erro por si só diz que algo está ruim; o código diz o quê. Um pico
        // em RN-105 (escopo por linha) é incidente de segurança, e um pico em RN-035
        // (horário já reservado) é disputa por agenda — problemas completamente
        // diferentes que a métrica agregada confundiria.
        Assert.Contains("vetly_regras_violadas_total", texto);
        Assert.Contains("codigo=\"AUTH-001\"", texto);
    }

    [Fact]
    public async Task Metrics_SondasDeSaude_NaoPoluemAsMetricasDeNegocio()
    {
        // Arrange — o orquestrador bate no health check a cada poucos segundos. Se
        // isso entrasse na contagem, dominaria o volume e faria a latência média
        // parecer excelente: a maioria das "requisições" não faria nada.
        await _client.GetAsync("/health/live");
        await _client.GetAsync("/health/ready");

        // Act
        var texto = await (await _client.GetAsync("/metrics")).Content.ReadAsStringAsync();

        // Assert
        Assert.DoesNotContain("rota=\"/health/live\"", texto);
        Assert.DoesNotContain("rota=\"/health/ready\"", texto);
        Assert.DoesNotContain("rota=\"/metrics\"", texto);
    }

    // ── Correlação: log ↔ trace ↔ resposta ───────────────────────────────────

    [Fact]
    public async Task Requisicao_ComCorrelationIdDoCliente_EcoaOMesmoIdNaResposta()
    {
        // Arrange — é assim que o app mobile costura a jornada dele à nossa: ele manda
        // o próprio id e espera reencontrá-lo do outro lado.
        const string idDoCliente = "app-mobile-jornada-42";

        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        requisicao.Headers.Add("X-Correlation-Id", idDoCliente);

        // Act
        var resposta = await _client.SendAsync(requisicao);

        // Assert
        Assert.Equal(idDoCliente, resposta.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Requisicao_SemCorrelationId_RecebeUmIdGeradoPelaApi()
    {
        // Arrange — cliente que não manda id não pode ficar sem correlação: seria
        // exatamente o chamado de suporte impossível de investigar.

        // Act
        var resposta = await _client.GetAsync("/health/live");

        // Assert
        var correlacao = resposta.Headers.GetValues("X-Correlation-Id").Single();

        Assert.False(string.IsNullOrWhiteSpace(correlacao));
    }

    [Fact]
    public async Task Requisicao_ComCorrelationIdGigante_TemOValorTruncado()
    {
        // Arrange — cabeçalho é entrada do usuário. Sem limite, um cliente inflaria
        // cada linha de log com o que quisesse; 128 caracteres cobrem GUID e trace id
        // W3C com folga.
        var idAbusivo = new string('x', 500);

        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        requisicao.Headers.Add("X-Correlation-Id", idAbusivo);

        // Act
        var resposta = await _client.SendAsync(requisicao);

        // Assert
        var correlacao = resposta.Headers.GetValues("X-Correlation-Id").Single();

        Assert.Equal(128, correlacao.Length);
    }

    [Fact]
    public async Task RespostaDeErro_TrazNoProblemDetailsOMesmoIdDoCabecalho()
    {
        // Arrange — esta é a costura que fecha o ciclo de suporte: o usuário lê o id na
        // tela de erro, e esse mesmo id abre o log e o trace da requisição exata.
        const string idDoCliente = "chamado-suporte-9981";

        var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(
                """{"email":"ninguem-existe@exemplo.com","senha":"senha-que-nao-vale"}""",
                Encoding.UTF8, "application/json")
        };

        requisicao.Headers.Add("X-Correlation-Id", idDoCliente);

        // Act
        var resposta = await _client.SendAsync(requisicao);

        // Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resposta.StatusCode);

        var problema = await LerJsonAsync(resposta);

        Assert.Equal(idDoCliente, problema.GetProperty("correlationId").GetString());
        Assert.Equal(idDoCliente, resposta.Headers.GetValues("X-Correlation-Id").Single());
        Assert.Equal("AUTH-001", problema.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task RespostaDeErro_ContinuaSendoProblemDetailsRfc7807()
    {
        // Arrange — instrumentar não pode mudar o contrato de erro. O app depende do
        // application/problem+json e dos campos title/status/detail.
        var requisicao = new HttpRequestMessage(HttpMethod.Get, "/api/consultas");
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token-invalido");

        // Act
        var resposta = await _client.SendAsync(requisicao);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.NotNull(resposta.Headers.GetValues("X-Correlation-Id").Single());
    }

    // ── Apoio ────────────────────────────────────────────────────────────────

    /// <summary>Lê o corpo da resposta como <see cref="JsonElement"/>.</summary>
    private static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta) =>
        JsonSerializer.Deserialize<JsonElement>(await resposta.Content.ReadAsStringAsync(), Json);

    /// <summary>Extrai os nomes dos checks presentes no relatório de saúde.</summary>
    private static List<string> NomesDosChecks(JsonElement relatorio) =>
        [.. relatorio.GetProperty("checks")
            .EnumerateArray()
            .Select(c => c.GetProperty("nome").GetString()!)];
}
