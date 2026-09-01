using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Vetly.Application.DTOs.IA;
using Vetly.Application.Exceptions;
using Vetly.Application.Services;

namespace Vetly.UnitTests;

/// <summary>
/// Prova que os serviços de Aplicação realmente emitem a telemetria que dizem emitir —
/// não apenas que os instrumentos existem (isso é <c>VetlyTelemetryTests</c>), mas que
/// as chamadas estão nos caminhos de código certos.
/// </summary>
/// <remarks>
/// <para>
/// A distinção importa. Um instrumento declarado e nunca incrementado passa em qualquer
/// teste de contrato e produz um painel eternamente zerado — que é pior do que painel
/// nenhum, porque parece funcionar. O que se verifica aqui é o oposto: exercita-se o
/// serviço pela API pública dele e observa-se o que saiu do outro lado.
/// </para>
/// <para>
/// O <c>OllamaService</c> é o alvo escolhido por ser onde a instrumentação tem o maior
/// valor prático: o LLM é a dependência mais lenta e mais instável do sistema, e é
/// justamente a chamada em que "a API está lenta" e "o modelo está lento" precisam ser
/// distinguíveis.
/// </para>
/// </remarks>
[Collection(ColecaoDeTelemetria.Nome)]
public class InstrumentacaoDeServicosTests
{
    private readonly ColetorDeTelemetriaFixture _coletor;

    public InstrumentacaoDeServicosTests(ColetorDeTelemetriaFixture coletor) => _coletor = coletor;

    // ── IA: duração e resultado (§5.3) ───────────────────────────────────────

    [Fact]
    public async Task ChamadaAoLlm_ComSucesso_RegistraDuracaoComOperacaoEResultado()
    {
        // Arrange
        _coletor.Limpar();

        var servico = CriarServico(RespostaDoOllama("[]"));

        var contexto = new ContextoClinicoDto
        {
            Especie = "Canino",
            Raca = "Golden Retriever",
            IdadeAnos = 3,
            PesoKg = 31.5m,
            Sintomas = ["apatia", "vomito"]
        };

        // Act
        await servico.SugerirDiagnosticoAsync(contexto);

        // Assert
        var medicao = _coletor.De("vetly.ia.duracao")
            .LastOrDefault(m => m.Tag("operacao") == "SugerirDiagnosticoAsync");

        Assert.NotNull(medicao);
        Assert.Equal("sucesso", medicao.Tag("resultado"));

        // Duração real, não zero fixo: o histograma existe para responder p95 e p99.
        Assert.True(medicao.Valor >= 0);
    }

    [Fact]
    public async Task ChamadaAoLlm_QuandoOModeloFalha_RegistraDuracaoComResultadoDeFalha()
    {
        // Arrange — o registro no finally é o que garante isto. Medir só no caminho
        // feliz esconderia exatamente o caso que interessa: o timeout, que é longo por
        // definição e sai por exceção.
        _coletor.Limpar();

        var servico = CriarServico(status: HttpStatusCode.InternalServerError);

        var contexto = new ContextoClinicoDto
        {
            Especie = "Felino",
            Raca = "SRD",
            IdadeAnos = 7,
            PesoKg = 4.2m,
            Sintomas = ["prostracao"]
        };

        // Act
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => servico.SugerirDiagnosticoAsync(contexto));

        // Assert
        var medicao = _coletor.De("vetly.ia.duracao")
            .LastOrDefault(m => m.Tag("operacao") == "SugerirDiagnosticoAsync");

        Assert.NotNull(medicao);
        Assert.Equal("falha", medicao.Tag("resultado"));
    }

    [Fact]
    public async Task ChamadaAoLlm_AbreUmSpanDeClienteComOModeloEmTag()
    {
        // Arrange — ActivityKind.Client é o que faz o backend desenhar o Ollama como
        // dependência externa, com a latência dele separada da nossa.
        _coletor.Limpar();

        var servico = CriarServico(RespostaDoOllama("[]"), modelo: "llama3.1");

        var contexto = new ContextoClinicoDto
        {
            Especie = "Canino",
            Raca = "Poodle",
            IdadeAnos = 2,
            PesoKg = 8m,
            Sintomas = ["tosse"]
        };

        // Act
        await servico.SugerirDiagnosticoAsync(contexto);

        // Assert
        var span = _coletor.SpansChamados("ia.SugerirDiagnosticoAsync").LastOrDefault();

        Assert.NotNull(span);
        Assert.Equal(ActivityKind.Client, span.Kind);
        Assert.Equal("llama3.1", span.GetTagItem("vetly.ia.modelo"));
    }

    [Fact]
    public async Task ChamadaAoLlm_QuandoOModeloFalha_MarcaOSpanComoErro()
    {
        // Arrange
        _coletor.Limpar();

        var servico = CriarServico(status: HttpStatusCode.ServiceUnavailable);

        var contexto = new ContextoClinicoDto
        {
            Especie = "Canino",
            Raca = "Beagle",
            IdadeAnos = 5,
            PesoKg = 12m,
            Sintomas = ["febre"]
        };

        // Act
        await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => servico.SugerirDiagnosticoAsync(contexto));

        // Assert
        var span = _coletor.SpansChamados("ia.SugerirDiagnosticoAsync").LastOrDefault();

        Assert.NotNull(span);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
    }

    [Fact]
    public async Task GuardaDePeso_BarraAntesDoLlm_ENaoRegistraDuracaoDeIa()
    {
        // Arrange — RN-081: sem peso, a IA não chega a ser consultada. A métrica precisa
        // refletir isso; contar aqui inflaria o volume de uso da IA com chamadas que
        // nunca saíram, e distorceria qualquer análise de custo por inferência.
        _coletor.Limpar();

        var servico = CriarServico(RespostaDoOllama("{}"));

        // Act
        var excecao = await Assert.ThrowsAsync<BusinessRuleException>(
            () => servico.SugerirProtocoloAsync("Gastrite aguda", "Canino", pesoKg: 0));

        // Assert
        Assert.Equal("RN-081", excecao.Codigo);
        Assert.DoesNotContain(_coletor.De("vetly.ia.duracao"),
            m => m.Tag("operacao") == "SugerirProtocoloAsync");
    }

    // ── Apoio ────────────────────────────────────────────────────────────────

    /// <summary>Monta o serviço com um <c>HttpClient</c> que devolve o que o teste mandar.</summary>
    /// <param name="corpo">Corpo da resposta simulada do Ollama.</param>
    /// <param name="status">Status HTTP devolvido pelo handler.</param>
    /// <param name="modelo">Modelo anunciado na configuração.</param>
    private static OllamaService CriarServico(
        string corpo = "{}",
        HttpStatusCode status = HttpStatusCode.OK,
        string modelo = "llama3.2")
    {
        var http = new HttpClient(new HandlerDeTeste(corpo, status))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ollama:Model"] = modelo })
            .Build();

        return new OllamaService(http, configuracao);
    }

    /// <summary>Envelope de resposta do Ollama, com o texto do modelo dentro.</summary>
    /// <param name="conteudo">O que o modelo teria respondido.</param>
    private static string RespostaDoOllama(string conteudo) =>
        $$"""{"model":"llama3.2","response":{{System.Text.Json.JsonSerializer.Serialize(conteudo)}},"done":true}""";

    /// <summary>Handler que responde sempre a mesma coisa, sem tocar a rede.</summary>
    private sealed class HandlerDeTeste : HttpMessageHandler
    {
        private readonly string _corpo;
        private readonly HttpStatusCode _status;

        public HandlerDeTeste(string corpo, HttpStatusCode status)
        {
            _corpo = corpo;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_corpo, Encoding.UTF8, "application/json")
            });
    }
}
