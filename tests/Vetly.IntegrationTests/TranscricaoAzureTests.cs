using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.Interfaces;
using Vetly.Application.Services;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Adapters;
using Vetly.Infrastructure.Jobs;

namespace Vetly.IntegrationTests;

/// <summary>
/// Transcricao pela Fast Transcription API do Azure Speech (§5.3).
///
/// Nenhum teste aqui chama o Azure de verdade: o motor e substituido por um
/// <see cref="HttpMessageHandler"/> falso. Consumir quota numa suite que roda a cada
/// commit seria caro e, pior, deixaria o resultado do teste depender da rede.
/// </summary>
public class TranscricaoAzureTests
{
    private const string Chave = "chave-de-teste";
    private const string Regiao = "canadacentral";

    /// <summary>Resposta minima de sucesso, para os testes que so precisam de um 200 valido.</summary>
    private const string RespostaComTexto = """
        {"durationMilliseconds":30000,
         "combinedPhrases":[{"text":"ok"}],
         "phrases":[{"offsetMilliseconds":0,"durationMilliseconds":1000,"text":"ok",
                     "confidence":0.9,"locale":"pt-BR"}]}
        """;

    private static ConfiguracaoDoAzureSpeech Configuracao(
        string? endpoint = null, string? versaoDaApi = null) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AZURE_SPEECH_REGION"] = Regiao,
            ["AZURE_SPEECH_KEY"] = Chave,
            ["AZURE_SPEECH_ENDPOINT"] = endpoint,
            ["AZURE_SPEECH_API_VERSION"] = versaoDaApi
        }).Build());

    private static SolicitarTranscricaoRequest Pedido(string formato = "audio/webm;codecs=opus") =>
        new(Guid.NewGuid(), Guid.NewGuid(), 0,
            "https://storage.vetly.test/api/storage/audio?sig=abc", formato, "pt-BR",
            "https://api.vetly.test/api/internos/stt/callback", "token-do-segmento");

    // ── Configuracao (§5.3) ──────────────────────────────────────────────────

    [Fact]
    public void Configuracao_SemEndpointConfigurado_DerivaODaRegiao()
    {
        var config = Configuracao();

        // A Fast Transcription mora sob Cognitive Services, e nao no host de short audio
        // (*.stt.speech.microsoft.com) que a API anterior usava
        Assert.Equal("https://canadacentral.api.cognitive.microsoft.com/", config.BaseUrl.ToString());
    }

    [Fact]
    public void Configuracao_ComEndpointConfigurado_UsaEleEmVezDaRegiao()
    {
        // O recurso de Speech pode estar num dominio proprio; derivar tudo da regiao
        // deixaria esse caso sem saida (§5.3)
        var config = Configuracao(endpoint: "https://vetly-speech.cognitiveservices.azure.com");

        Assert.Equal("https://vetly-speech.cognitiveservices.azure.com/", config.BaseUrl.ToString());
    }

    [Fact]
    public void Configuracao_EndpointSemBarraFinal_GanhaABarra()
    {
        // O HttpClient resolve caminho relativo contra a BaseAddress: sem a barra, o
        // ultimo segmento do host seria descartado e a chamada iria para o lugar errado
        var config = Configuracao(endpoint: "https://vetly-speech.cognitiveservices.azure.com");

        Assert.EndsWith("/", config.BaseUrl.ToString());
    }

    [Fact]
    public void Configuracao_MontaOCaminhoDaTranscricaoRapidaComAVersaoDaApi()
    {
        var caminho = Configuracao().CaminhoDeTranscricao();

        Assert.StartsWith("speechtotext/transcriptions:transcribe", caminho);
        Assert.Contains($"api-version={ConfiguracaoDoAzureSpeech.VersaoDaApiPadrao}", caminho);
    }

    [Fact]
    public void Configuracao_VersaoDaApi_ESobrescrivelPorAmbiente()
    {
        // 2025-10-15 habilita phraseList (vocabulario veterinario): subir de versao nao
        // pode exigir deploy de codigo
        var config = Configuracao(versaoDaApi: "2025-10-15");

        Assert.Equal("2025-10-15", config.VersaoDaApi);
        Assert.Contains("api-version=2025-10-15", config.CaminhoDeTranscricao());
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

    // ── Traducao da resposta (§5.3) ──────────────────────────────────────────

    [Fact]
    public async Task Sucesso_ViraCallbackComTextoConfiancaMediaEMotor()
    {
        const string corpo = """
            {"durationMilliseconds":30000,
             "combinedPhrases":[{"text":"paciente com vomito ha um dia, abdome doloroso"}],
             "phrases":[
               {"offsetMilliseconds":0,"durationMilliseconds":4200,
                "text":"paciente com vomito ha um dia","confidence":0.9,"locale":"pt-BR"},
               {"offsetMilliseconds":4200,"durationMilliseconds":3000,
                "text":"abdome doloroso","confidence":0.8,"locale":"pt-BR"}]}
            """;

        var callback = await ExecutarAsync(Pedido(), HttpStatusCode.OK, corpo);

        Assert.Equal("Ok", callback.Status);
        Assert.Equal("paciente com vomito ha um dia, abdome doloroso", callback.Texto);

        // Media das frases: uma frase curta e muito confiante nao responde pelo trecho inteiro
        Assert.Equal(0.85m, callback.Confianca);

        Assert.Equal("azure-speech", callback.Motor!.Nome);
        Assert.Equal(ConfiguracaoDoAzureSpeech.VersaoDaApiPadrao, callback.Motor.Versao);

        // O token do segmento volta com o callback: e o par que prova que a transcricao
        // responde ao trecho que foi mandado (RN-009)
        Assert.Equal("token-do-segmento", callback.CallbackToken);
    }

    [Fact]
    public async Task Sucesso_PreencheOsTrechosComMarcacaoDeTempo()
    {
        const string corpo = """
            {"durationMilliseconds":30000,
             "combinedPhrases":[{"text":"febre e apatia"}],
             "phrases":[{"offsetMilliseconds":120,"durationMilliseconds":2400,
                         "text":"febre e apatia","confidence":0.77,"locale":"pt-BR"}]}
            """;

        var callback = await ExecutarAsync(Pedido(), HttpStatusCode.OK, corpo);

        // O campo existe no contrato desde sempre (§5.3) e vinha SEMPRE nulo com a API de
        // short audio, que nao devolvia marcacao de tempo. E o que permite conferir cada
        // frase do rascunho contra o audio.
        Assert.NotNull(callback.Trechos);
        Assert.Contains("offsetMilliseconds", callback.Trechos);
        Assert.Contains("120", callback.Trechos);
        Assert.Contains("febre e apatia", callback.Trechos);
    }

    [Fact]
    public async Task Sucesso_SemFraseComConfianca_NaoInventaZero()
    {
        // Ausencia de dado nao e incerteza do motor: zero puxaria a media para baixo por
        // um motivo que nao existe
        const string corpo = """
            {"combinedPhrases":[{"text":"apatia"}],
             "phrases":[{"offsetMilliseconds":0,"durationMilliseconds":900,"text":"apatia"}]}
            """;

        var callback = await ExecutarAsync(Pedido(), HttpStatusCode.OK, corpo);

        Assert.Equal("Ok", callback.Status);
        Assert.Null(callback.Confianca);
    }

    [Fact]
    public async Task SemCombinedPhrases_ViraFalhaPorAudioIlegivel()
    {
        var callback = await ExecutarAsync(
            Pedido(), HttpStatusCode.OK, """{"durationMilliseconds":30000,"combinedPhrases":[]}""");

        // Mesmo desfecho que o NoMatch da API anterior, pelo mesmo motivo: reenviar o
        // mesmo trecho de silencio tres vezes so gastaria quota para chegar ao mesmo lugar
        Assert.Equal("Falha", callback.Status);
        Assert.Equal(MotivoFalhaTranscricao.AudioIlegivel, callback.Motivo);
        Assert.Null(callback.Texto);
    }

    [Fact]
    public async Task CombinedPhrasesComTextoVazio_ViraFalhaEmVezDeTranscricaoEmBranco()
    {
        // Sem esta guarda, entraria no prontuario uma transcricao em branco com cara de
        // sucesso
        var callback = await ExecutarAsync(
            Pedido(), HttpStatusCode.OK, """{"combinedPhrases":[{"text":""}],"phrases":[]}""");

        Assert.Equal("Falha", callback.Status);
        Assert.Equal(MotivoFalhaTranscricao.AudioIlegivel, callback.Motivo);
    }

    [Fact]
    public async Task RespostaVazia_ViraFalhaPorAudioIlegivel()
    {
        var callback = await ExecutarAsync(Pedido(), HttpStatusCode.OK, string.Empty);

        Assert.Equal(MotivoFalhaTranscricao.AudioIlegivel, callback.Motivo);
    }

    // ── Traducao do status HTTP (§5.3) ───────────────────────────────────────

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task ErroDeCliente_ViraFalhaPorFormatoNaoSuportado(HttpStatusCode status)
    {
        // 400 e 422 sao erro de cliente na doc da Microsoft: reenviar o mesmo audio daria
        // o mesmo resultado
        var callback = await ExecutarAsync(Pedido(), status, """{"error":"bad request"}""");

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

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Http429E5xx_ViramFalhaPorMotorIndisponivelParaOBackoffRetentar(HttpStatusCode status)
    {
        // Transitorios: vale reenviar com espera
        var callback = await ExecutarAsync(Pedido(), status, string.Empty);

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
    public async Task Webm_EAceitoETranscreve()
    {
        var motor = new MotorFalso(HttpStatusCode.OK, RespostaComTexto);

        // O MediaRecorder do Chromium so grava WebM: recusa-lo aqui, como a API de short
        // audio obrigava, deixava a captura funcionando apenas no Firefox
        var callback = await ExecutarAsync(Pedido("audio/webm;codecs=opus"), motor);

        Assert.Equal("Ok", callback.Status);
        Assert.Equal("ok", callback.Texto);
        Assert.Equal(1, motor.ChamadasAoAzure);
    }

    [Theory]
    [InlineData("audio/webm;codecs=opus", "audio/webm")]
    [InlineData("audio/webm", "audio/webm")]
    [InlineData("audio/ogg;codecs=opus", "audio/ogg")]
    [InlineData("audio/wav", "audio/wav")]
    [InlineData("audio/mpeg", "audio/mpeg")]
    [InlineData("audio/flac", "audio/flac")]
    public async Task FormatosDaFastTranscription_SaoAceitos(string formato, string contentTypeEsperado)
    {
        var motor = new MotorFalso(HttpStatusCode.OK, RespostaComTexto);

        var callback = await ExecutarAsync(Pedido(formato), motor);

        Assert.Equal("Ok", callback.Status);
        Assert.Equal(contentTypeEsperado, motor.ContentTypeDoAudio);
    }

    [Theory]
    [InlineData("audio/mp4")]
    [InlineData("video/webm")]
    [InlineData("")]
    public async Task FormatoForaDaLista_ERecusadoAntesDeGastarUmaChamada(string formato)
    {
        var motor = new MotorFalso(HttpStatusCode.OK, RespostaComTexto);

        var callback = await ExecutarAsync(Pedido(formato), motor);

        // Recusar aqui da o motivo certo de graca, em vez de gastar chamada e
        // retentativas para o segmento morrer com um motivo generico
        Assert.Equal(MotivoFalhaTranscricao.FormatoNaoSuportado, callback.Motivo);
        Assert.Equal(0, motor.ChamadasAoAzure);
    }

    // ── Forma da requisicao (§5.3) ───────────────────────────────────────────

    [Fact]
    public async Task Requisicao_VaiEmMultipartComOAudioEADefinicao()
    {
        var motor = new MotorFalso(HttpStatusCode.OK, RespostaComTexto);

        await ExecutarAsync(Pedido(), motor);

        // O audio vai inline: e o que dispensa a URL publica que a API de short audio exigia
        Assert.Contains("multipart/form-data", motor.ContentTypeDaRequisicao);
        Assert.Contains("name=audio", motor.CorpoEnviado.Replace("\"", string.Empty));
        Assert.Contains("name=definition", motor.CorpoEnviado.Replace("\"", string.Empty));
        Assert.Contains("bytes-de-audio", motor.CorpoEnviado);
    }

    [Fact]
    public async Task Requisicao_ADefinicaoPedePtBrESemFiltroDePalavrao()
    {
        var motor = new MotorFalso(HttpStatusCode.OK, RespostaComTexto);

        await ExecutarAsync(Pedido(), motor);

        Assert.Contains("""["pt-BR"]""", motor.CorpoEnviado);

        // None e nao Masked: prontuario clinico com asteriscos no lugar do que foi dito
        // deixa de ser registro fiel do atendimento
        Assert.Contains("""profanityFilterMode":"None""", motor.CorpoEnviado);
    }

    [Fact]
    public async Task Requisicao_LevaAChaveEOCaminhoDaTranscricaoRapida()
    {
        var motor = new MotorFalso(HttpStatusCode.OK, RespostaComTexto);

        await ExecutarAsync(Pedido(), motor);

        Assert.Equal(Chave, motor.ChaveEnviada);
        Assert.Equal(
            "https://canadacentral.api.cognitive.microsoft.com/speechtotext/transcriptions:transcribe",
            motor.UrlChamada!.GetLeftPart(UriPartial.Path));
        Assert.Contains(
            $"api-version={ConfiguracaoDoAzureSpeech.VersaoDaApiPadrao}", motor.UrlChamada.Query);
    }

    // ── Fecho do ciclo pelo servico de captura (§5.3) ────────────────────────

    [Fact]
    public async Task Handler_EntregaOTextoPeloMesmoRegistrarCallbackDoFluxoHttp()
    {
        var captura = new Mock<ICapturaService>();
        var motor = new MotorFalso(HttpStatusCode.OK, RespostaComTexto);

        await ExecutarAsync(Pedido(), motor, captura);

        // Mesma maquina de estados, mesma idempotencia, mesma verificacao de token —
        // apenas sem o salto de rede
        captura.Verify(c => c.RegistrarCallbackAsync(
            It.Is<CallbackDeTranscricaoDto>(d => d.Status == "Ok" && d.Texto == "ok")), Times.Once);
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

    // ── O que o motivo da falha provoca na fila (§4.2) ───────────────────────

    [Fact]
    public async Task Http429_DevolveOSegmentoAFilaComBackoff()
    {
        var cenario = new CenarioDeCaptura();

        await ExecutarComCapturaRealAsync(cenario, HttpStatusCode.TooManyRequests);

        // Transitorio: ainda ha tentativa, entao o trecho volta para a fila em vez de ser
        // dado como perdido
        Assert.Equal(EstadoSegmentoAudio.Recebido, cenario.Segmento.Estado);
        Assert.Equal(MotivoFalhaTranscricao.MotorIndisponivel, cenario.Segmento.FalhaMotivo);

        cenario.Fila.Verify(f => f.EnfileirarAsync(
            TipoJob.TranscreverSegmento, cenario.Segmento.Id.ToString(),
            CapturaService.BackoffDaTentativa(cenario.Segmento.Tentativas)), Times.Once);
    }

    [Fact]
    public async Task Http400_NaoRetentaIndefinidamente()
    {
        // Esgotadas as tentativas, o trecho e dado como perdido e o rascunho sai sem ele,
        // com aviso — reenviar para sempre um pedido que o Azure ja recusou trocaria uma
        // sessao travada por um laco de jobs
        var cenario = new CenarioDeCaptura(tentativas: SegmentoAudio.MaximoDeTentativas);

        await ExecutarComCapturaRealAsync(cenario, HttpStatusCode.BadRequest);

        Assert.Equal(EstadoSegmentoAudio.Falha, cenario.Segmento.Estado);
        Assert.Equal(MotivoFalhaTranscricao.FormatoNaoSuportado, cenario.Segmento.FalhaMotivo);

        cenario.Fila.Verify(f => f.EnfileirarAsync(
            TipoJob.TranscreverSegmento, It.IsAny<string>(), It.IsAny<TimeSpan?>()), Times.Never);
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

        await ExecutarComCapturaAsync(pedido, motor, captura.Object);

        Assert.NotNull(recebido);
        return recebido!;
    }

    /// <summary>O caminho real: adaptador enfileira, handler consome o payload e chama a captura.</summary>
    private static async Task ExecutarComCapturaAsync(
        SolicitarTranscricaoRequest pedido, MotorFalso motor, ICapturaService captura)
    {
        var adaptador = new SttAdapterAzure(
            FilaQueGuardaOPayload(out var payload), NullLogger<SttAdapterAzure>.Instance);

        await adaptador.SolicitarTranscricaoAsync(pedido);

        var handler = new TranscreverSegmentoAzureHandler(
            new FabricaDeClientes(motor), Configuracao(), captura,
            NullLogger<TranscreverSegmentoAzureHandler>.Instance);

        await handler.ExecutarAsync(
            new Job(TipoJob.TranscreverSegmentoAzure, payload()), CancellationToken.None);
    }

    /// <summary>
    /// Roda o handler contra o <see cref="CapturaService"/> de verdade.
    ///
    /// O que se quer ver aqui não é a tradução do status, que os testes acima já cobrem,
    /// e sim o que ela <b>provoca</b>: o mesmo motivo que vira aviso na tela decide se o
    /// trecho volta para a fila ou é dado como perdido.
    /// </summary>
    private static async Task ExecutarComCapturaRealAsync(CenarioDeCaptura cenario, HttpStatusCode status)
    {
        var pedido = new SolicitarTranscricaoRequest(
            cenario.Segmento.Id, cenario.Sessao.ConsultaId, 0,
            "https://storage.vetly.test/api/storage/audio?sig=abc", "audio/webm;codecs=opus", "pt-BR",
            "https://api.vetly.test/api/internos/stt/callback", CenarioDeCaptura.Token);

        await ExecutarComCapturaAsync(pedido, new MotorFalso(status, string.Empty), cenario.Servico());
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

    /// <summary>Sessão e segmento já despachados, com o serviço de captura de verdade por trás.</summary>
    private sealed class CenarioDeCaptura
    {
        public const string Token = "token-do-segmento";

        public SessaoCaptura Sessao { get; }
        public SegmentoAudio Segmento { get; }
        public Mock<IFilaDeJobs> Fila { get; } = new();

        private readonly Mock<ICapturaRepository> _repo = new();

        public CenarioDeCaptura(int tentativas = 1)
        {
            Sessao = new SessaoCaptura(Guid.NewGuid(), capturaAtiva: true);
            Segmento = new SegmentoAudio(Sessao.Id, 0, Guid.NewGuid(), 30000, 0);

            // O servico so aceita o callback que traz o token do proprio segmento (RN-009)
            for (var i = 0; i < tentativas; i++)
                Segmento.RegistrarDespacho(HashDoToken(Token), DateTime.UtcNow);

            _repo.Setup(r => r.ObterSegmentoAsync(Segmento.Id)).ReturnsAsync(Segmento);
            _repo.Setup(r => r.ObterSessaoAsync(Sessao.Id)).ReturnsAsync(Sessao);
            _repo.Setup(r => r.ObterSegmentosAsync(Sessao.Id)).ReturnsAsync([Segmento]);
            _repo.Setup(r => r.SalvarAsync()).ReturnsAsync(1);
        }

        public CapturaService Servico() => new(
            _repo.Object, Mock.Of<IConsultaRepository>(), Mock.Of<IVeterinarioRepository>(),
            Mock.Of<IEmpresaRepository>(), Mock.Of<IAnimalRepository>(), Mock.Of<IMidiaRepository>(),
            Fila.Object, Mock.Of<IUsuarioAtual>());

        private static string HashDoToken(string token) =>
            Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
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
                cliente.BaseAddress = new Uri($"https://{Regiao}.api.cognitive.microsoft.com/");
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
        public string ContentTypeDaRequisicao { get; private set; } = string.Empty;
        public string CorpoEnviado { get; private set; } = string.Empty;
        public string? ContentTypeDoAudio { get; private set; }
        public string? ChaveEnviada { get; private set; }
        public Uri? UrlChamada { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // O download do audio e um GET; a transcricao e um POST
            if (request.Method == HttpMethod.Get)
            {
                if (ErroNoDownload is not null)
                    throw ErroNoDownload;

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent("bytes-de-audio"u8.ToArray())
                };
            }

            if (ErroNoAzure is not null)
                throw ErroNoAzure;

            ChamadasAoAzure++;
            UrlChamada = request.RequestUri;

            if (request.Content is MultipartFormDataContent multipart)
            {
                ContentTypeDaRequisicao = multipart.Headers.ContentType?.ToString() ?? string.Empty;

                // O corpo inteiro do multipart, com as fronteiras: e onde se ve que as
                // duas partes sairam e o que cada uma levou
                CorpoEnviado = await multipart.ReadAsStringAsync(cancellationToken);

                ContentTypeDoAudio = ContentTypeDaParte(multipart, "audio");
            }

            if (request.Headers.TryGetValues("Ocp-Apim-Subscription-Key", out var chaves))
                ChaveEnviada = chaves.FirstOrDefault();

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_corpo, Encoding.UTF8, "application/json")
            };
        }

        /// <summary>Lido cru: o cabeçalho de cada parte não passa pelo parser do .NET.</summary>
        private static string? ContentTypeDaParte(MultipartFormDataContent multipart, string nome)
        {
            var parte = multipart.FirstOrDefault(
                p => string.Equals(NomeDaParte(p), nome, StringComparison.Ordinal));

            return parte is not null && parte.Headers.TryGetValues("Content-Type", out var tipos)
                ? tipos.FirstOrDefault()
                : null;
        }

        private static string? NomeDaParte(HttpContent parte) =>
            parte.Headers.ContentDisposition?.Name?.Trim(AspasDuplas);

        private const char AspasDuplas = '"';
    }
}
