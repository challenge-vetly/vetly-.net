using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Vetly.Application.DTOs.IA;
using Vetly.Application.Exceptions;
using Vetly.Application.Services;

namespace Vetly.UnitTests;

/// <summary>
/// Testes unitarios do OllamaService.
/// Usa HttpMessageHandler mockado para evitar dependencia do Ollama real.
/// </summary>
public class OllamaServiceTests
{
    private static IConfiguration CriarConfig(string model = "llama3.2")
    {
        var values = new Dictionary<string, string?> { ["Ollama:Model"] = model };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static HttpClient CriarHttpClientComResposta(string jsonResposta)
    {
        var handler = new MockHttpMessageHandler(jsonResposta);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task SugerirProtocolo_SemPeso_LancaBusinessRuleExceptionRN081(decimal pesoKg)
    {
        // Handler que falha se chamado: a guarda tem que barrar antes de consultar a IA
        var http = new HttpClient(new MockHttpMessageHandler(BuildOllamaResponse("{}")))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
        var service = new OllamaService(http, CriarConfig());

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.SugerirProtocoloAsync("Gastrite aguda", "Canino", pesoKg));

        Assert.Equal("RN-081", ex.Codigo);
    }

    [Fact]
    public async Task SugerirDiagnostico_RetornaListaVazia_QuandoRespostaFormatadaIncorretamente()
    {
        // Simula Ollama retornando texto sem JSON valido
        var respostaOllama = BuildOllamaResponse("texto sem json aqui");
        var http = CriarHttpClientComResposta(respostaOllama);
        var service = new OllamaService(http, CriarConfig());

        var contexto = new ContextoClinicoDto
        {
            Especie = "Canino", Raca = "Labrador", IdadeAnos = 3,
            PesoKg = 25, Sintomas = ["vomito", "apatia"]
        };

        var resultado = await service.SugerirDiagnosticoAsync(contexto);

        Assert.Empty(resultado);
    }

    [Fact]
    public async Task SugerirDiagnostico_RetornaHipoteses_QuandoOllamaRetornaJsonValido()
    {
        var hipoteses = new[]
        {
            new { hipotese = "Gastrite", nivelConfianca = "medio", justificativa = "Vomito e apatia" }
        };
        var jsonArray = JsonSerializer.Serialize(hipoteses);
        var respostaOllama = BuildOllamaResponse(jsonArray);

        var http = CriarHttpClientComResposta(respostaOllama);
        var service = new OllamaService(http, CriarConfig());

        var contexto = new ContextoClinicoDto
        {
            Especie = "Canino", Raca = "Labrador", IdadeAnos = 3,
            PesoKg = 25, Sintomas = ["vomito"]
        };

        var resultado = await service.SugerirDiagnosticoAsync(contexto);

        Assert.Single(resultado);
        Assert.Equal("Gastrite", resultado[0].Hipotese);
    }

    [Fact]
    public async Task RealizarTriagem_RetornaIndeterminado_QuandoJsonInvalido()
    {
        var respostaOllama = BuildOllamaResponse("nao e json");
        var http = CriarHttpClientComResposta(respostaOllama);
        var service = new OllamaService(http, CriarConfig());

        var sintomas = new SintomasDto { Especie = "Felino", Sintomas = ["tosse"] };

        var resultado = await service.RealizarTriagemAsync(sintomas);

        Assert.Equal("Indeterminado", resultado.NivelUrgencia);
    }

    [Fact]
    public async Task GerarOrientacoes_RetornaTexto_QuandoOllamaResponde()
    {
        var textoEsperado = "Administre o medicamento a cada 8 horas com alimento.";
        var respostaOllama = BuildOllamaResponse(textoEsperado);
        var http = CriarHttpClientComResposta(respostaOllama);
        var service = new OllamaService(http, CriarConfig());

        var consulta = new ConsultaResumoDto
        {
            Especie = "Canino", Diagnostico = "Gastrite leve",
            Medicamentos = ["Omeprazol 20mg"], Conduta = "Dieta hipoalergenica"
        };

        var resultado = await service.GerarOrientacoesPostAtendimentoAsync(consulta);

        Assert.Equal(textoEsperado, resultado);
    }

    // Monta o payload de resposta do Ollama: { "response": "..." }
    private static string BuildOllamaResponse(string responseText) =>
        JsonSerializer.Serialize(new { response = responseText });

    // Handler HTTP mockado — retorna sempre 200 OK com o JSON configurado e guarda o
    // payload enviado, que e onde se ve o prompt montado e as opcoes do modelo
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public MockHttpMessageHandler(string responseJson) => _responseJson = responseJson;

        /// <summary>Corpo da última requisição ao Ollama, cru.</summary>
        public string PayloadEnviado { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                PayloadEnviado = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
        }
    }

    // ── Teto de tokens da resposta (§5.4) ────────────────────────────────────

    /// <summary>Sonda que devolve sempre a mesma resposta e guarda o que foi pedido.</summary>
    private static (OllamaService servico, MockHttpMessageHandler sonda) ServicoComSonda(
        string resposta = "{}")
    {
        var sonda = new MockHttpMessageHandler(BuildOllamaResponse(resposta));
        var http = new HttpClient(sonda) { BaseAddress = new Uri("http://localhost:11434") };

        return (new OllamaService(http, CriarConfig()), sonda);
    }

    private static ContextoDaEstruturacaoDto ContextoDaEstruturacao(decimal? pesoKg = 31.5m) => new()
    {
        Transcricao = "Paciente apresenta vomito ha um dia, abdome doloroso a palpacao.",
        Especie = "Canino",
        Raca = "Golden Retriever",
        IdadeAnos = 3,
        PesoKg = pesoKg
    };

    private static int NumPredictDe(string payload) =>
        JsonDocument.Parse(payload).RootElement.GetProperty("options").GetProperty("num_predict").GetInt32();

    private static string PromptDe(string payload) =>
        JsonDocument.Parse(payload).RootElement.GetProperty("prompt").GetString()!;

    [Fact]
    public async Task EstruturarConsulta_PedeTetoDeTokensMaior()
    {
        var (servico, sonda) = ServicoComSonda();

        await servico.EstruturarConsultaAsync(ContextoDaEstruturacao());

        // O prontuario estruturado tem cinco campos e um deles e texto corrido: com 500
        // o JSON trunca no meio, o parse falha e o texto bruto cai na anamnese — e o
        // veterinario le "a IA nao estruturou", que e o sintoma errado
        Assert.Equal(1500, NumPredictDe(sonda.PayloadEnviado));
    }

    [Fact]
    public async Task SugerirDiagnostico_SegueNoTetoPadrao()
    {
        var (servico, sonda) = ServicoComSonda("[]");

        await servico.SugerirDiagnosticoAsync(new ContextoClinicoDto
        {
            Especie = "Canino", Raca = "Labrador", IdadeAnos = 3, PesoKg = 25, Sintomas = ["vomito"]
        });

        // Uma lista de tres hipoteses cabe folgada em 500: subir o teto aqui so gastaria
        // tempo de inferencia
        Assert.Equal(500, NumPredictDe(sonda.PayloadEnviado));
    }

    [Fact]
    public async Task SugerirProtocolo_SegueNoTetoPadrao()
    {
        var (servico, sonda) = ServicoComSonda();

        await servico.SugerirProtocoloAsync("Gastrite aguda", "Canino", 31.5m);

        Assert.Equal(500, NumPredictDe(sonda.PayloadEnviado));
    }

    [Fact]
    public async Task RealizarTriagem_SegueNoTetoPadrao()
    {
        var (servico, sonda) = ServicoComSonda();

        await servico.RealizarTriagemAsync(new SintomasDto { Especie = "Felino", Sintomas = ["tosse"] });

        Assert.Equal(500, NumPredictDe(sonda.PayloadEnviado));
    }

    [Fact]
    public async Task GerarOrientacoes_SegueNoTetoPadrao()
    {
        var (servico, sonda) = ServicoComSonda("orientacoes");

        await servico.GerarOrientacoesPostAtendimentoAsync(new ConsultaResumoDto
        {
            Especie = "Canino", Diagnostico = "Gastrite leve",
            Medicamentos = ["Omeprazol"], Conduta = "Dieta branda"
        });

        Assert.Equal(500, NumPredictDe(sonda.PayloadEnviado));
    }

    // ── Peso ausente na estruturacao (RN-081) ────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(0d)]
    [InlineData(-1d)]
    public async Task EstruturarConsulta_SemPeso_ProibeDoseNoPrompt(double? pesoKg)
    {
        var (servico, sonda) = ServicoComSonda();

        await servico.EstruturarConsultaAsync(ContextoDaEstruturacao((decimal?)pesoKg));

        var prompt = PromptDe(sonda.PayloadEnviado);

        // O campo `conduta` e exatamente onde a posologia aparece. O aviso PesoAusente no
        // rascunho alerta a interface, mas nao impede o texto de sair pronto e ser levado
        // tal e qual para a receita — a guarda tem de estar no prompt (RN-081)
        Assert.Contains("NAO sugira dose", prompt);
        Assert.Contains("a dose depende do peso", prompt);
        Assert.Contains("Peso: nao informado", prompt);
    }

    [Fact]
    public async Task EstruturarConsulta_ComPeso_NaoProibeDose()
    {
        var (servico, sonda) = ServicoComSonda();

        await servico.EstruturarConsultaAsync(ContextoDaEstruturacao(31.5m));

        var prompt = PromptDe(sonda.PayloadEnviado);

        // Com peso cadastrado a posologia e justamente o que se quer da IA: manter a
        // proibicao aqui esvaziaria a conduta sem motivo
        Assert.DoesNotContain("NAO sugira dose", prompt);

        // Formatado como o servico formata: fixar "31,5" aqui amarraria o teste a
        // cultura da maquina que o roda
        Assert.Contains($"Peso: {31.5m} kg", prompt);
    }
}
