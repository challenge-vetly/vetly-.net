using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Vetly.Application.DTOs.IA;
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
        Assert.NotEmpty(resultado.Disclaimer); // RN-100: disclaimer sempre presente, mesmo em falha de parsing
    }

    [Fact]
    public async Task RealizarTriagem_JsonValido_SempreIncluiDisclaimerFixo()
    {
        var jsonResposta = JsonSerializer.Serialize(new
        {
            nivelUrgencia = "Baixo",
            recomendacao = "Observe o animal por 24h.",
            possiveisCausas = new[] { "Alimentacao inadequada" }
        });
        var http = CriarHttpClientComResposta(BuildOllamaResponse(jsonResposta));
        var service = new OllamaService(http, CriarConfig());

        var resultado = await service.RealizarTriagemAsync(new SintomasDto { Especie = "Canino", Sintomas = ["apatia"] });

        Assert.NotEmpty(resultado.Disclaimer);
        Assert.Contains("diagnostico", resultado.Disclaimer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RealizarTriagem_NivelUrgenciaEmergencia_OrientaAtendimentoPresencialImediato()
    {
        var jsonResposta = JsonSerializer.Serialize(new
        {
            nivelUrgencia = "Emergencia",
            recomendacao = "Sangramento intenso observado.",
            possiveisCausas = new[] { "Trauma" }
        });
        var http = CriarHttpClientComResposta(BuildOllamaResponse(jsonResposta));
        var service = new OllamaService(http, CriarConfig());

        var resultado = await service.RealizarTriagemAsync(new SintomasDto { Especie = "Canino", Sintomas = ["sangramento"] });

        Assert.Contains("presencial", resultado.Recomendacao, StringComparison.OrdinalIgnoreCase);
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

    // Handler HTTP mockado — retorna sempre 200 OK com o JSON configurado
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public MockHttpMessageHandler(string responseJson) => _responseJson = responseJson;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
