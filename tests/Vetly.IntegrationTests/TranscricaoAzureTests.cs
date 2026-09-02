using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Adapters;
using Vetly.Infrastructure.Jobs;

namespace Vetly.IntegrationTests;

/// <summary>
/// Transcricao pelo Azure Speech (§5.3).
///
/// Nenhum teste aqui chama o Azure de verdade: o motor e substituido por um
/// <see cref="HttpMessageHandler"/> falso. Consumir quota numa suite que roda a cada
/// commit seria caro e, pior, deixaria o resultado do teste depender da rede.
/// </summary>
public class TranscricaoAzureTests
{
    private const string Chave = "chave-de-teste";
    private const string Regiao = "canadacentral";

    private static ConfiguracaoDoAzureSpeech Configuracao() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AZURE_SPEECH_REGION"] = Regiao,
            ["AZURE_SPEECH_KEY"] = Chave
        }).Build());

    private static SolicitarTranscricaoRequest Pedido(string formato = "audio/ogg;codecs=opus") =>
        new(Guid.NewGuid(), Guid.NewGuid(), 0,
            "https://storage.vetly.test/api/storage/audio?sig=abc", formato, "pt-BR",
            "https://api.vetly.test/api/internos/stt/callback", "token-do-segmento");

    // ── Configuracao (§5.3) ──────────────────────────────────────────────────

    [Fact]
    public void Configuracao_MontaOEndpointDeReconhecimentoDaRegiao()
    {
        var config = Configuracao();

        // O host de reconhecimento e o *.stt.speech.microsoft.com; o
        // *.api.cognitive.microsoft.com generico responde 404 ao reconhecimento
        Assert.Equal("https://canadacentral.stt.speech.microsoft.com/", config.BaseUrl.ToString());

        var caminho = config.CaminhoDeReconhecimento("pt-BR");

        Assert.StartsWith("speech/recognition/conversation/cognitiveservices/v1", caminho);
        Assert.Contains("language=pt-BR", caminho);
        Assert.Contains("format=detailed", caminho);
    }

    [Fact]
    public void Configuracao_SemChave_NaoSobe()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AZURE_SPEECH_REGION"] = Regiao
        }).Build();

        // A chave vem so de variavel de ambiente; sem ela e melhor nao subir do que
        // subir para descobrir pelo 401 no primeiro segmento
        var erro = Assert.Throws<InvalidOperationException>(() => new ConfiguracaoDoAzureSpeech(config));

        Assert.Contains("AZURE_SPEECH_KEY", erro.Message);
    }

    [Fact]
    public void Configuracao_SemRegiao_NaoSobe()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AZURE_SPEECH_KEY"] = Chave
        }).Build();

        Assert.Throws<InvalidOperationException>(() => new ConfiguracaoDoAzureSpeech(config));
    }

    // ── Adaptador: aceita e processa depois (§5.3) ───────────────────────────

    [Fact]
    public async Task Adaptador_EnfileiraOTrabalhoEmVezDeChamarOAzureNaHora()
    {
        var fila = new Mock<IFilaDeJobs>();
        string? payload = null;

        fila.Setup(f => f.EnfileirarAsync(
                TipoJob.TranscreverSegmentoAzure, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<TipoJob, string?, TimeSpan?>((_, p, _) => payload = p)
            .Returns(Task.CompletedTask);

        var adaptador = new SttAdapterAzure(fila.Object, NullLogger<SttAdapterAzure>.Instance);
        var pedido = Pedido();

        // "Aceito, processo depois": transcrever dentro do despacho prenderia o
        // veterinario esperando o Azure para poder seguir o atendimento
        Assert.True(await adaptador.SolicitarTranscricaoAsync(pedido));
        Assert.NotNull(payload);
        Assert.Contains(pedido.SegmentoId.ToString(), payload);
        Assert.Contains("token-do-segmento", payload);
    }

    [Fact]
    public async Task Adaptador_SemConseguirEnfileirar_DevolveFalsoEmVezDeEstourar()
    {
        var fila = new Mock<IFilaDeJobs>();
        fila.Setup(f => f.EnfileirarAsync(
                It.IsAny<TipoJob>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .ThrowsAsync(new InvalidOperationException("banco fora"));

        var adaptador = new SttAdapterAzure(fila.Object, NullLogger<SttAdapterAzure>.Instance);

        // Falso faz o job de despacho ser retentado com espera crescente, em vez de dar
        // o segmento como perdido
        Assert.False(await adaptador.SolicitarTranscricaoAsync(Pedido()));
    }

    // ── Traducao da resposta do Azure (§5.3) ─────────────────────────────────

    [Fact]
    public async Task Sucesso_ViraCallbackOkComTextoConfiancaEMotor()
    {
        const string corpo = """
            {"RecognitionStatus":"Success","DisplayText":"paciente com vomito ha um dia",
             "NBest":[{"Confidence":0.93,"Display":"paciente com vomito ha um dia",
                       "Lexical":"paciente com vomito ha um dia"}]}
            """;

        var callback = await ExecutarAsync(Pedido(), HttpStatusCode.OK, corpo);

        Assert.Equal("Ok", callback.Status);
        Assert.Equal("paciente com vomito ha um dia", callback.Texto);
        Assert.Equal(0.93m, callback.Confianca);
        Assert.Equal("azure-speech", callback.Motor!.Nome);
        Assert.Equal("v1", callback.Motor.Versao);

        // O token do segmento volta com o callback: e o par que prova que a transcricao
        // responde ao trecho que foi mandado (RN-009)
        Assert.Equal("token-do-segmento", callback.CallbackToken);
    }

    [Fact]
    public async Task Sucesso_SemDisplayText_UsaOTextoDoNBest()
    {
        // No formato detailed o Azure entrega o texto em NBest[0].Display
        const string corpo = """
            {"RecognitionStatus":"Success","NBest":[{"Confidence":0.81,"Display":"febre e apatia"}]}
            """;

        var callback = await ExecutarAsync(Pedido(), HttpStatusCode.OK, corpo);

        Assert.Equal("Ok", callback.Status);
        Assert.Equal("febre e apatia", callback.Texto);
    }

    [Theory]
    [InlineData("NoMatch")]
    [InlineData("InitialSilenceTimeout")]
    [InlineData("BabbleTimeout")]
    public async Task SemFalaReconhecida_ViraFalhaPorAudioIlegivel(string status)
    {
        var callback = await ExecutarAsync(
            Pedido(), HttpStatusCode.OK, $$"""{"RecognitionStatus":"{{status}}"}""");

        // Nao e motor fora do ar: reenviar o mesmo trecho de silencio tres vezes so
        // gastaria quota para chegar ao mesmo lugar
        Assert.Equal("Falha", callback.Status);
        Assert.Equal(MotivoFalhaTranscricao.AudioIlegivel, callback.Motivo);
    }

    [Fact]
    public async Task SucessoComTextoVazio_ViraFalhaEmVezDeTranscricaoEmBranco()
    {
        // Resposta REAL do Azure para um container que ele nao le (WebM/Opus, medido
        // contra o servico): 200, "Success", texto vazio e confianca zero. Formato nao
        // suportado nao falha — ele emudece. Sem esta guarda, entraria no prontuario
        // uma transcricao em branco com cara de sucesso.
        const string corpo = """
            {"RecognitionStatus":"Success","DisplayText":"",
             "NBest":[{"Confidence":0.0,"Lexical":"","ITN":"","MaskedITN":"","Display":""}]}
            """;

        var callback = await ExecutarAsync(Pedido(), HttpStatusCode.OK, corpo);

        Assert.Equal("Falha", callback.Status);
        Assert.Equal(MotivoFalhaTranscricao.AudioIlegivel, callback.Motivo);
        Assert.Null(callback.Texto);
    }

    [Fact]
    public async Task Http400_ViraFalhaPorFormatoNaoSuportado()
    {
        var callback = await ExecutarAsync(Pedido(), HttpStatusCode.BadRequest, "{\"error\":\"bad audio\"}");

        Assert.Equal("Falha", callback.Status);
        Assert.Equal(MotivoFalhaTranscricao.FormatoNaoSuportado, callback.Motivo);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Http401E403_ViramFalhaPorMotorIndisponivel(HttpStatusCode status)
    {
        var callback = await ExecutarAsync(Pedido(), status, string.Empty);

        // Credencial errada nao e audio ruim — a diferenca e entre "esta consulta perdeu
        // um trecho" e "nenhuma consulta transcreve ate alguem arrumar a chave"
        Assert.Equal("Falha", callback.Status);
        Assert.Equal(MotivoFalhaTranscricao.MotorIndisponivel, callback.Motivo);
    }

    [Fact]
    public async Task Http429_ViraFalhaPorMotorIndisponivelParaOBackoffRetentar()
    {
        var callback = await ExecutarAsync(Pedido(), HttpStatusCode.TooManyRequests, string.Empty);

        Assert.Equal(MotivoFalhaTranscricao.MotorIndisponivel, callback.Motivo);
    }

    [Fact]
    public async Task Http500_ViraFalhaPorMotorIndisponivel()
    {
        var callback = await ExecutarAsync(Pedido(), HttpStatusCode.InternalServerError, string.Empty);

        Assert.Equal(MotivoFalhaTranscricao.MotorIndisponivel, callback.Motivo);
    }

    [Fact]
    public async Task ExcecaoDeRede_ViraFalhaPorMotorIndisponivel()
    {
        var callback = await ExecutarAsync(
            Pedido(), HttpStatusCode.OK, string.Empty, erroNoAzure: new HttpRequestException("conexao recusada"));

        Assert.Equal(MotivoFalhaTranscricao.MotorIndisponivel, callback.Motivo);
    }

    [Fact]
    public async Task Timeout_ViraFalhaPorMotorIndisponivel()
    {
        var callback = await ExecutarAsync(
            Pedido(), HttpStatusCode.OK, string.Empty, erroNoAzure: new TaskCanceledException("timeout"));

        Assert.Equal(MotivoFalhaTranscricao.MotorIndisponivel, callback.Motivo);
    }

    [Fact]
    public async Task AudioQueNaoBaixa_ViraFalhaPorMotorIndisponivel()
    {
        var callback = await ExecutarAsync(
            Pedido(), HttpStatusCode.OK, string.Empty, erroNoDownload: new HttpRequestException("storage fora"));

        Assert.Equal(MotivoFalhaTranscricao.MotorIndisponivel, callback.Motivo);
    }

    // ── Formato do audio ─────────────────────────────────────────────────────

    [Fact]
    public async Task Webm_ERecusadoAntesDeGastarUmaChamada()
    {
        var motor = new MotorFalso(HttpStatusCode.OK, string.Empty);
        var callback = await ExecutarAsync(Pedido("audio/webm;codecs=opus"), motor);

        // A REST API de short audio nao aceita WebM: mandar assim voltaria como 400
        // generico e sumiria no meio dos outros erros
        Assert.Equal(MotivoFalhaTranscricao.FormatoNaoSuportado, callback.Motivo);
        Assert.Equal(0, motor.ChamadasAoAzure);
    }

    [Theory]
    [InlineData("audio/ogg;codecs=opus", "audio/ogg; codecs=opus")]
    [InlineData("audio/ogg", "audio/ogg; codecs=opus")]
    [InlineData("audio/wav", "audio/wav; codecs=audio/pcm; samplerate=16000")]
    public async Task ContentType_VaiNoCabecalhoQueOAzureEspera(string formato, string esperado)
    {
        var motor = new MotorFalso(HttpStatusCode.OK, """{"RecognitionStatus":"Success","DisplayText":"ok"}""");

        await ExecutarAsync(Pedido(formato), motor);

        Assert.Equal(esperado, motor.ContentTypeEnviado);
    }

    [Fact]
    public async Task Requisicao_LevaAChaveEOEndpointDeReconhecimento()
    {
        var motor = new MotorFalso(HttpStatusCode.OK, """{"RecognitionStatus":"Success","DisplayText":"ok"}""");

        await ExecutarAsync(Pedido(), motor);

        Assert.Equal(Chave, motor.ChaveEnviada);
        Assert.Equal(
            "https://canadacentral.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1",
            motor.UrlChamada!.GetLeftPart(UriPartial.Path));
    }

    // ── Fecho do ciclo pelo servico de captura (§5.3) ────────────────────────

    [Fact]
    public async Task Handler_EntregaOTextoPeloMesmoRegistrarCallbackDoFluxoHttp()
    {
        var captura = new Mock<ICapturaService>();
        var motor = new MotorFalso(
            HttpStatusCode.OK, """{"RecognitionStatus":"Success","DisplayText":"conteudo"}""");

        await ExecutarAsync(Pedido(), motor, captura);

        // Mesma maquina de estados, mesma idempotencia, mesma verificacao de token —
        // apenas sem o salto de rede
        captura.Verify(c => c.RegistrarCallbackAsync(
            It.Is<CallbackDeTranscricaoDto>(d => d.Status == "Ok" && d.Texto == "conteudo")), Times.Once);
    }

    [Fact]
    public async Task Handler_PayloadVazio_Falha()
    {
        var handler = new TranscreverSegmentoAzureHandler(
            new FabricaDeClientes(new MotorFalso(HttpStatusCode.OK, string.Empty)),
            Configuracao(), Mock.Of<ICapturaService>(),
            NullLogger<TranscreverSegmentoAzureHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecutarAsync(
            new Job(TipoJob.TranscreverSegmentoAzure, null), CancellationToken.None));
    }

    // ── Andaimes ─────────────────────────────────────────────────────────────

    private static async Task<CallbackDeTranscricaoDto> ExecutarAsync(
        SolicitarTranscricaoRequest pedido,
        HttpStatusCode status,
        string corpo,
        Exception? erroNoAzure = null,
        Exception? erroNoDownload = null)
    {
        var motor = new MotorFalso(status, corpo)
        {
            ErroNoAzure = erroNoAzure,
            ErroNoDownload = erroNoDownload
        };

        return await ExecutarAsync(pedido, motor);
    }

    private static async Task<CallbackDeTranscricaoDto> ExecutarAsync(
        SolicitarTranscricaoRequest pedido, MotorFalso motor, Mock<ICapturaService>? captura = null)
    {
        captura ??= new Mock<ICapturaService>();
        CallbackDeTranscricaoDto? recebido = null;

        captura.Setup(c => c.RegistrarCallbackAsync(It.IsAny<CallbackDeTranscricaoDto>()))
            .Callback<CallbackDeTranscricaoDto>(d => recebido = d)
            .Returns(Task.CompletedTask);

        var adaptador = new SttAdapterAzure(FilaQueGuardaOPayload(out var payload), NullLogger<SttAdapterAzure>.Instance);
        await adaptador.SolicitarTranscricaoAsync(pedido);

        var handler = new TranscreverSegmentoAzureHandler(
            new FabricaDeClientes(motor), Configuracao(), captura.Object,
            NullLogger<TranscreverSegmentoAzureHandler>.Instance);

        await handler.ExecutarAsync(
            new Job(TipoJob.TranscreverSegmentoAzure, payload()), CancellationToken.None);

        Assert.NotNull(recebido);
        return recebido!;
    }

    /// <summary>Fila que só guarda o payload — o caminho real do adaptador até o handler.</summary>
    private static IFilaDeJobs FilaQueGuardaOPayload(out Func<string?> payload)
    {
        string? guardado = null;
        payload = () => guardado;

        var fila = new Mock<IFilaDeJobs>();
        fila.Setup(f => f.EnfileirarAsync(
                It.IsAny<TipoJob>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<TipoJob, string?, TimeSpan?>((_, p, _) => guardado = p)
            .Returns(Task.CompletedTask);

        return fila.Object;
    }

    /// <summary>
    /// Entrega o mesmo <see cref="MotorFalso"/> nos dois clientes que o handler pede:
    /// o do Azure (nomeado) e o do download do áudio (sem nome).
    /// </summary>
    private sealed class FabricaDeClientes : IHttpClientFactory
    {
        private readonly MotorFalso _motor;

        public FabricaDeClientes(MotorFalso motor) => _motor = motor;

        public HttpClient CreateClient(string name)
        {
            var cliente = new HttpClient(_motor, disposeHandler: false);

            // So o cliente do Azure leva a chave. Mandar a credencial junto do download
            // do audio seria entrega-la a outro destino.
            if (name == ConfiguracaoDoAzureSpeech.NomeDoHttpClient)
            {
                cliente.BaseAddress = new Uri($"https://{Regiao}.stt.speech.microsoft.com/");
                cliente.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", Chave);
            }

            return cliente;
        }
    }

    /// <summary>Azure de mentira: responde o que o teste mandar e registra o que recebeu.</summary>
    private sealed class MotorFalso : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _corpo;

        public MotorFalso(HttpStatusCode status, string corpo)
        {
            _status = status;
            _corpo = corpo;
        }

        public Exception? ErroNoAzure { get; init; }
        public Exception? ErroNoDownload { get; init; }

        public int ChamadasAoAzure { get; private set; }
        public string? ContentTypeEnviado { get; private set; }
        public string? ChaveEnviada { get; private set; }
        public Uri? UrlChamada { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // O download do audio e um GET; o reconhecimento e um POST
            if (request.Method == HttpMethod.Get)
            {
                if (ErroNoDownload is not null)
                    throw ErroNoDownload;

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent("bytes-de-audio"u8.ToArray())
                });
            }

            if (ErroNoAzure is not null)
                throw ErroNoAzure;

            ChamadasAoAzure++;
            UrlChamada = request.RequestUri;
            // Lido cru: o cabecalho do WAV nao passa pelo parser do .NET
                ContentTypeEnviado = request.Content is not null &&
                                     request.Content.Headers.TryGetValues("Content-Type", out var tipos)
                    ? tipos.FirstOrDefault()
                    : null;

            if (request.Headers.TryGetValues("Ocp-Apim-Subscription-Key", out var chaves))
                ChaveEnviada = chaves.FirstOrDefault();

            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_corpo, Encoding.UTF8, "application/json")
            });
        }
    }
}
