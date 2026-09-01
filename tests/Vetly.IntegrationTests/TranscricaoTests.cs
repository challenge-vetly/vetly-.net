using System.Net;
using System.Text;
using System.Text.Json;
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
/// Despacho ao motor de transcricao e volta pelo callback (RN-009, §5.3).
///
/// Fica neste projeto porque handlers e adaptadores vivem na Infrastructure.
/// </summary>
public class TranscricaoTests : IClassFixture<VetlyWebApplicationFactory>
{
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public TranscricaoTests(VetlyWebApplicationFactory factory) => _client = factory.CreateClient();

    private static IConfiguration Config() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Servicos:CallbackBaseUrl"] = "https://api.vetly.test"
        }).Build();

    private static (SessaoCaptura sessao, SegmentoAudio segmento, Midia midia) Cenario()
    {
        var sessao = new SessaoCaptura(Guid.NewGuid(), capturaAtiva: true);
        var midia = new Midia(TipoMidia.AudioConsulta, "audio/webm", consultaId: sessao.ConsultaId);
        var segmento = new SegmentoAudio(sessao.Id, 0, midia.Id, 30000, 0);

        return (sessao, segmento, midia);
    }

    private static (Mock<ICapturaRepository>, Mock<IMidiaRepository>, Mock<IStorageAdapter>) Dependencias(
        SessaoCaptura sessao, SegmentoAudio segmento, Midia midia)
    {
        var captura = new Mock<ICapturaRepository>();
        captura.Setup(r => r.ObterSegmentoAsync(segmento.Id)).ReturnsAsync(segmento);
        captura.Setup(r => r.ObterSessaoAsync(sessao.Id)).ReturnsAsync(sessao);
        captura.Setup(r => r.SalvarAsync()).ReturnsAsync(1);

        var midias = new Mock<IMidiaRepository>();
        midias.Setup(r => r.ObterPorIdAsync(midia.Id)).ReturnsAsync(midia);

        var storage = new Mock<IStorageAdapter>();
        storage.Setup(s => s.GerarUrlDeLeituraAsync(midia.ChaveStorage, It.IsAny<TimeSpan>()))
            .ReturnsAsync(new UrlAssinadaDto("https://storage.test/audio?sig=abc", DateTime.UtcNow.AddMinutes(15)));

        return (captura, midias, storage);
    }

    // ── Despacho ao motor (§5.3) ─────────────────────────────────────────────

    [Fact]
    public async Task Despacho_EntregaOAudioPorUrlTemporariaEGuardaSoOHashDoToken()
    {
        var (sessao, segmento, midia) = Cenario();
        var (captura, midias, storage) = Dependencias(sessao, segmento, midia);

        SolicitarTranscricaoRequest pedido = default;
        var stt = new Mock<ISttAdapter>();
        stt.Setup(s => s.SolicitarTranscricaoAsync(It.IsAny<SolicitarTranscricaoRequest>()))
            .Callback<SolicitarTranscricaoRequest>(r => pedido = r).ReturnsAsync(true);

        var handler = new TranscreverSegmentoHandler(
            captura.Object, midias.Object, storage.Object, stt.Object, Config(),
            NullLogger<TranscreverSegmentoHandler>.Instance);

        await handler.ExecutarAsync(
            new Job(TipoJob.TranscreverSegmento, segmento.Id.ToString()), CancellationToken.None);

        // A API nao proxia bytes: o motor busca o audio direto no storage (RN-090)
        Assert.Equal("https://storage.test/audio?sig=abc", pedido.AudioUrl);
        Assert.Equal("https://api.vetly.test/api/internos/stt/callback", pedido.CallbackUrl);
        Assert.Equal(sessao.ConsultaId, pedido.ConsultaId);

        Assert.Equal(EstadoSegmentoAudio.Enviado, segmento.Estado);
        Assert.Equal(1, segmento.Tentativas);

        // Guardar o hash, e nao o token, evita que vazamento da tabela permita forjar
        // uma transcricao
        Assert.NotNull(segmento.CallbackTokenHash);
        Assert.NotEqual(pedido.CallbackToken, segmento.CallbackTokenHash);
    }

    [Fact]
    public async Task Despacho_SegmentoQueJaTeveDesfecho_NaoVaiAoMotorDeNovo()
    {
        var (sessao, segmento, midia) = Cenario();
        var (captura, midias, storage) = Dependencias(sessao, segmento, midia);
        segmento.RegistrarTranscricao();

        var stt = new Mock<ISttAdapter>();

        var handler = new TranscreverSegmentoHandler(
            captura.Object, midias.Object, storage.Object, stt.Object, Config(),
            NullLogger<TranscreverSegmentoHandler>.Instance);

        await handler.ExecutarAsync(
            new Job(TipoJob.TranscreverSegmento, segmento.Id.ToString()), CancellationToken.None);

        // O callback pode ter chegado antes deste job rodar
        stt.Verify(s => s.SolicitarTranscricaoAsync(It.IsAny<SolicitarTranscricaoRequest>()), Times.Never);
    }

    [Fact]
    public async Task Despacho_MotorRecusa_FalhaOJobParaQueOWorkerRetente()
    {
        var (sessao, segmento, midia) = Cenario();
        var (captura, midias, storage) = Dependencias(sessao, segmento, midia);

        var stt = new Mock<ISttAdapter>();
        stt.Setup(s => s.SolicitarTranscricaoAsync(It.IsAny<SolicitarTranscricaoRequest>())).ReturnsAsync(false);

        var handler = new TranscreverSegmentoHandler(
            captura.Object, midias.Object, storage.Object, stt.Object, Config(),
            NullLogger<TranscreverSegmentoHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecutarAsync(
            new Job(TipoJob.TranscreverSegmento, segmento.Id.ToString()), CancellationToken.None));

        // Motor fora do ar nao derruba a consulta: o job e retentado com espera crescente
        Assert.Equal(MotivoFalhaTranscricao.MotorIndisponivel, segmento.FalhaMotivo);
        Assert.Equal(EstadoSegmentoAudio.Recebido, segmento.Estado);
    }

    [Fact]
    public async Task Despacho_PayloadInvalido_Falha()
    {
        var handler = new TranscreverSegmentoHandler(
            Mock.Of<ICapturaRepository>(), Mock.Of<IMidiaRepository>(), Mock.Of<IStorageAdapter>(),
            Mock.Of<ISttAdapter>(), Config(), NullLogger<TranscreverSegmentoHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecutarAsync(
            new Job(TipoJob.TranscreverSegmento, "nao-e-guid"), CancellationToken.None));
    }

    // ── Motor simulado (§5.3) ────────────────────────────────────────────────

    [Fact]
    public async Task MotorSimulado_DevolveOTextoPeloMesmoCallbackDoFluxoReal()
    {
        var fila = new Mock<IFilaDeJobs>();
        string? payload = null;
        TimeSpan? atraso = null;

        fila.Setup(f => f.EnfileirarAsync(TipoJob.TranscreverSegmentoSimulado, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<TipoJob, string?, TimeSpan?>((_, p, a) => { payload = p; atraso = a; })
            .Returns(Task.CompletedTask);

        var adapter = new SttAdapterSimulado(fila.Object, NullLogger<SttAdapterSimulado>.Instance);

        var aceito = await adapter.SolicitarTranscricaoAsync(new SolicitarTranscricaoRequest(
            Guid.NewGuid(), Guid.NewGuid(), 2, "https://storage.test/a", "audio/webm", "pt-BR",
            "https://api.vetly.test/api/internos/stt/callback", "token"));

        Assert.True(aceito);

        // Com atraso, para exercitar o fluxo assincrono de verdade — a transcricao nao
        // pode aparecer dentro da propria requisicao
        Assert.Equal(SttAdapterSimulado.AtrasoDaTranscricao, atraso);

        var callback = JsonSerializer.Deserialize<CallbackDeTranscricaoDto>(payload!, Json)!;
        Assert.Equal("Ok", callback.Status);

        // Texto sintetico marcado: nunca deve ser confundido com fala real numa demo
        Assert.Contains("transcricao simulada", callback.Texto);

        // Varia com a sequencia, para que a juncao dos trechos seja verificavel
        Assert.Contains("trecho 2", callback.Texto);
    }

    [Fact]
    public async Task MotorSimulado_EntregaOCallbackAoServicoDeCaptura()
    {
        var segmentoId = Guid.NewGuid();
        var captura = new Mock<ICapturaService>();

        var handler = new TranscreverSegmentoSimuladoHandler(
            captura.Object, NullLogger<TranscreverSegmentoSimuladoHandler>.Instance);

        var payload = $$"""{"segmentoId":"{{segmentoId}}","status":"Ok","texto":"conteudo"}""";
        await handler.ExecutarAsync(
            new Job(TipoJob.TranscreverSegmentoSimulado, payload), CancellationToken.None);

        captura.Verify(c => c.RegistrarCallbackAsync(It.Is<CallbackDeTranscricaoDto>(
            d => d.SegmentoId == segmentoId && d.Texto == "conteudo")), Times.Once);
    }

    [Fact]
    public async Task MotorSimulado_PayloadVazio_Falha()
    {
        var handler = new TranscreverSegmentoSimuladoHandler(
            Mock.Of<ICapturaService>(), NullLogger<TranscreverSegmentoSimuladoHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecutarAsync(
            new Job(TipoJob.TranscreverSegmentoSimulado, null), CancellationToken.None));
    }

    // ── Estruturacao pela IA (RN-080) ────────────────────────────────────────

    [Fact]
    public async Task Estruturacao_ChamaAGeracaoDoRascunhoDaSessao()
    {
        var sessaoId = Guid.NewGuid();
        var rascunhos = new Mock<IRascunhoService>();

        var handler = new EstruturarConsultaHandler(
            rascunhos.Object, NullLogger<EstruturarConsultaHandler>.Instance);

        await handler.ExecutarAsync(
            new Job(TipoJob.EstruturarConsulta, sessaoId.ToString()), CancellationToken.None);

        rascunhos.Verify(r => r.GerarAsync(sessaoId), Times.Once);
    }

    [Fact]
    public async Task Estruturacao_PayloadInvalido_Falha()
    {
        var handler = new EstruturarConsultaHandler(
            Mock.Of<IRascunhoService>(), NullLogger<EstruturarConsultaHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecutarAsync(
            new Job(TipoJob.EstruturarConsulta, "nao-e-guid"), CancellationToken.None));
    }

    [Fact]
    public async Task Rascunho_SemToken_NaoEAlcancavel()
    {
        var resposta = await _client.GetAsync($"/api/consultas/{Guid.NewGuid()}/rascunho");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    // ── Fluxo Node-RED (§5.3) ────────────────────────────────────────────────

    private static SttAdapterNodeRed NodeRed(HttpStatusCode status, out RespostaFixa fixa)
    {
        fixa = new RespostaFixa(status);
        var http = new HttpClient(fixa) { BaseAddress = new Uri("http://node-red.test") };

        return new SttAdapterNodeRed(http, NullLogger<SttAdapterNodeRed>.Instance);
    }

    [Fact]
    public async Task NodeRed_FluxoAceita_ODespachoEDadoComoFeito()
    {
        var adapter = NodeRed(HttpStatusCode.Accepted, out var fixa);

        var aceito = await adapter.SolicitarTranscricaoAsync(new SolicitarTranscricaoRequest(
            Guid.NewGuid(), Guid.NewGuid(), 0, "https://storage.test/a", "audio/webm", "pt-BR",
            "https://api.vetly.test/api/internos/stt/callback", "token-do-callback"));

        Assert.True(aceito);
        Assert.Equal("/vetly/stt", fixa.UltimoCaminho);

        // O token do callback vai na ida: e o que amarra a volta ao segmento
        Assert.Contains("token-do-callback", fixa.UltimoCorpo);
    }

    [Fact]
    public async Task NodeRed_FluxoRecusa_DevolveFalsoEmVezDeEstourar()
    {
        var adapter = NodeRed(HttpStatusCode.InternalServerError, out _);

        var aceito = await adapter.SolicitarTranscricaoAsync(new SolicitarTranscricaoRequest(
            Guid.NewGuid(), Guid.NewGuid(), 0, "https://storage.test/a", "audio/webm", "pt-BR",
            "https://api.vetly.test/api/internos/stt/callback", "token"));

        Assert.False(aceito);
    }

    [Fact]
    public async Task NodeRed_ForaDoAr_DevolveFalsoEmVezDeEstourar()
    {
        var http = new HttpClient(new RespostaFixa(new HttpRequestException("conexao recusada")))
        {
            BaseAddress = new Uri("http://node-red.test")
        };

        var adapter = new SttAdapterNodeRed(http, NullLogger<SttAdapterNodeRed>.Instance);

        var aceito = await adapter.SolicitarTranscricaoAsync(new SolicitarTranscricaoRequest(
            Guid.NewGuid(), Guid.NewGuid(), 0, "https://storage.test/a", "audio/webm", "pt-BR",
            "https://api.vetly.test/api/internos/stt/callback", "token"));

        // Motor fora do ar nao pode derrubar a consulta: o segmento volta para a fila
        Assert.False(aceito);
    }

    // ── Callback interno (§5.3) ──────────────────────────────────────────────

    [Fact]
    public async Task Callback_SemTokenDeServico_ERecusado()
    {
        var corpo = new StringContent(
            $$"""{"segmentoId":"{{Guid.NewGuid()}}","status":"Ok","texto":"x"}""",
            Encoding.UTF8, "application/json");

        var resposta = await _client.PostAsync("/api/internos/stt/callback", corpo);

        // A rota interna nao aceita chamada sem autenticacao de servico
        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Captura_SemToken_NaoEAlcancavel()
    {
        var consultaId = Guid.NewGuid();

        var iniciar = await _client.PostAsync($"/api/consultas/{consultaId}/iniciar", content: null);
        var estado = await _client.GetAsync($"/api/consultas/{consultaId}/captura");
        var encerrar = await _client.PostAsync($"/api/consultas/{consultaId}/encerrar", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, iniciar.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, estado.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, encerrar.StatusCode);
    }

    /// <summary>Resposta fixa para exercitar o adaptador sem um Node-RED de verdade.</summary>
    private sealed class RespostaFixa : HttpMessageHandler
    {
        private readonly HttpStatusCode? _status;
        private readonly Exception? _erro;

        public string? UltimoCaminho { get; private set; }
        public string UltimoCorpo { get; private set; } = string.Empty;

        public RespostaFixa(HttpStatusCode status) => _status = status;
        public RespostaFixa(Exception erro) => _erro = erro;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_erro is not null)
                throw _erro;

            UltimoCaminho = request.RequestUri?.AbsolutePath;

            if (request.Content is not null)
                UltimoCorpo = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status!.Value);
        }
    }

    // ── RN-009: o token do segmento fecha a volta ───────────────────────────

    [Fact]
    public async Task Simulado_DevolveOMesmoTokenQueRecebeu()
    {
        var fila = new Mock<IFilaDeJobs>();
        string? payload = null;

        fila.Setup(f => f.EnfileirarAsync(
                TipoJob.TranscreverSegmentoSimulado, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<TipoJob, string, TimeSpan?>((_, p, _) => payload = p)
            .Returns(Task.CompletedTask);

        var adaptador = new SttAdapterSimulado(fila.Object, NullLogger<SttAdapterSimulado>.Instance);

        var token = "token-do-segmento-abc123";

        await adaptador.SolicitarTranscricaoAsync(new SolicitarTranscricaoRequest(
            Guid.NewGuid(), Guid.NewGuid(), 0, "https://storage.test/audio", "audio/webm",
            "pt-BR", "https://api.vetly.test/api/internos/stt/callback", token));

        Assert.NotNull(payload);

        var callback = JsonSerializer.Deserialize<CallbackDeTranscricaoDto>(payload!, Json);

        // Sem devolver o token, o simulado passaria por uma porta que o fluxo de
        // producao nao atravessa, e a guarda so seria exercitada em producao
        Assert.Equal(token, callback!.CallbackToken);
    }

    [Fact]
    public async Task DespachoESimulado_OTokenDoPedidoBateComOHashGravado()
    {
        var (sessao, segmento, midia) = Cenario();
        var (captura, midias, storage) = Dependencias(sessao, segmento, midia);

        var fila = new Mock<IFilaDeJobs>();
        string? payload = null;

        fila.Setup(f => f.EnfileirarAsync(
                TipoJob.TranscreverSegmentoSimulado, It.IsAny<string>(), It.IsAny<TimeSpan?>()))
            .Callback<TipoJob, string, TimeSpan?>((_, p, _) => payload = p)
            .Returns(Task.CompletedTask);

        var stt = new SttAdapterSimulado(fila.Object, NullLogger<SttAdapterSimulado>.Instance);

        var handler = new TranscreverSegmentoHandler(
            captura.Object, midias.Object, storage.Object, stt, Config(),
            NullLogger<TranscreverSegmentoHandler>.Instance);

        await handler.ExecutarAsync(
            new Job(TipoJob.TranscreverSegmento, segmento.Id.ToString()), CancellationToken.None);

        var callback = JsonSerializer.Deserialize<CallbackDeTranscricaoDto>(payload!, Json);

        var hashDoTokenDevolvido = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(callback!.CallbackToken!))).ToLowerInvariant();

        // O caminho de ida grava o hash; o de volta traz o token. E o par que prova
        // que a transcricao responde ao trecho que foi mandado.
        Assert.Equal(segmento.CallbackTokenHash, hashDoTokenDevolvido);
    }
}
