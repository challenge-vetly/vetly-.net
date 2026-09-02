using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Adapters;

namespace Vetly.Infrastructure.Jobs;

/// <summary>
/// Transcreve um segmento no Azure Speech e devolve o texto pelo caminho de sempre
/// (§5.3).
///
/// Roda fora da requisição porque a chamada ao Azure leva segundos e o veterinário não
/// pode ficar esperando por ela para seguir o atendimento.
///
/// <b>Fecha o ciclo chamando <c>ICapturaService.RegistrarCallbackAsync</c> por dentro</b>,
/// sem dar a volta pela rede: é a mesma máquina de estados, a mesma idempotência, a
/// mesma verificação de token por segmento e os mesmos testes do caminho por callback
/// HTTP — apenas sem o salto. Postar no próprio <c>/api/internos/stt/callback</c>
/// exigiria que o processo soubesse se alcançar pela rede, carregaria o token de
/// serviço para dentro do worker e trocaria uma chamada de método por um ponto a mais
/// que pode cair; nada disso compra corretude nenhuma.
/// </summary>
public class TranscreverSegmentoAzureHandler : IJobHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Tipos MIME que a REST API de short audio aceita, mapeados para o cabeçalho
    /// exato que ela espera.
    ///
    /// A lista é fechada de propósito, e o motivo é pior do que "o Azure devolve 400":
    /// medido contra o serviço real, um WebM/Opus válido volta com <b>HTTP 200 e
    /// <c>RecognitionStatus: "Success"</c> com texto vazio</b> — e o Content-Type
    /// declarado não muda nada, porque o Azure inspeciona o container, não o cabeçalho.
    /// Um formato não suportado não falha: ele <b>emudece</b>.
    ///
    /// Sem esta lista, o trecho viraria <c>AudioIlegivel</c> depois de gastar chamada e
    /// retentativas, e o veterinário leria "áudio ilegível" quando o problema é o
    /// container. Recusar aqui dá o motivo certo (<c>FormatoNaoSuportado</c>) de graça.
    /// </summary>
    private static readonly Dictionary<string, string> ContentTypeDoAzure = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/ogg"] = "audio/ogg; codecs=opus",
        ["audio/ogg;codecs=opus"] = "audio/ogg; codecs=opus",
        ["audio/ogg; codecs=opus"] = "audio/ogg; codecs=opus",
        ["audio/wav"] = "audio/wav; codecs=audio/pcm; samplerate=16000",
        ["audio/wave"] = "audio/wav; codecs=audio/pcm; samplerate=16000",
        ["audio/x-wav"] = "audio/wav; codecs=audio/pcm; samplerate=16000"
    };

    private readonly IHttpClientFactory _clientes;
    private readonly ConfiguracaoDoAzureSpeech _azure;
    private readonly ICapturaService _captura;
    private readonly ILogger<TranscreverSegmentoAzureHandler> _logger;

    public TranscreverSegmentoAzureHandler(
        IHttpClientFactory clientes,
        ConfiguracaoDoAzureSpeech azure,
        ICapturaService captura,
        ILogger<TranscreverSegmentoAzureHandler> logger)
    {
        _clientes = clientes;
        _azure = azure;
        _captura = captura;
        _logger = logger;
    }

    /// <inheritdoc/>
    public TipoJob Tipo => TipoJob.TranscreverSegmentoAzure;

    /// <inheritdoc/>
    public async Task ExecutarAsync(Job job, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(job.Payload))
            throw new InvalidOperationException("Payload do job do Azure Speech esta vazio.");

        var req = JsonSerializer.Deserialize<SolicitarTranscricaoRequest>(job.Payload, Json);

        if (req.SegmentoId == Guid.Empty)
            throw new InvalidOperationException("Payload do job do Azure Speech nao identifica o segmento.");

        var callback = await TranscreverAsync(req, cancellationToken);

        await _captura.RegistrarCallbackAsync(callback);
    }

    /// <summary>
    /// Busca o áudio, manda ao Azure e traduz a resposta para o contrato da Vetly.
    ///
    /// Nunca propaga exceção: toda falha vira um callback com motivo. Deixar estourar
    /// faria o job ser retentado sem que o segmento soubesse o que aconteceu — e é o
    /// estado do segmento que destrava (ou não) a sessão.
    /// </summary>
    private async Task<CallbackDeTranscricaoDto> TranscreverAsync(
        SolicitarTranscricaoRequest req, CancellationToken cancellationToken)
    {
        if (!ContentTypeDoAzure.TryGetValue(NormalizarFormato(req.Formato), out var contentType))
        {
            _logger.LogWarning(
                "Segmento {SegmentoId} veio em '{Formato}', que a REST API de short audio nao aceita. " +
                "Formatos aceitos: WAV/PCM e OGG/OPUS, 16 kHz mono.", req.SegmentoId, req.Formato);

            return Falha(req, MotivoFalhaTranscricao.FormatoNaoSuportado);
        }

        byte[] audio;

        try
        {
            using var download = _clientes.CreateClient();
            audio = await download.GetByteArrayAsync(req.AudioUrl, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            // O audio esta no proprio storage da Vetly: falhar aqui e problema nosso, e
            // nao do Azure — mas o desfecho para o segmento e o mesmo, e a retentativa
            // com espera crescente cobre a indisponibilidade passageira.
            _logger.LogWarning(ex,
                "Nao foi possivel baixar o audio do segmento {SegmentoId} em {Url}.", req.SegmentoId, req.AudioUrl);

            return Falha(req, MotivoFalhaTranscricao.MotorIndisponivel);
        }

        try
        {
            using var conteudo = new ByteArrayContent(audio);

            // Sem validacao de proposito: o cabecalho que o Azure documenta para WAV e
            // "audio/wav; codecs=audio/pcm; samplerate=16000", e o parser do .NET recusa
            // a barra em "audio/pcm" sem aspas. Quem manda no formato aqui e o servico
            // que vai ler, nao a nossa leitura do RFC.
            conteudo.Headers.TryAddWithoutValidation("Content-Type", contentType);

            var cliente = _clientes.CreateClient(ConfiguracaoDoAzureSpeech.NomeDoHttpClient);

            using var resposta = await cliente.PostAsync(
                _azure.CaminhoDeReconhecimento(req.Idioma), conteudo, cancellationToken);

            if (!resposta.IsSuccessStatusCode)
                return FalhaDeHttp(req, resposta.StatusCode);

            var corpo = await resposta.Content.ReadFromJsonSafeAsync(cancellationToken);

            return Traduzir(req, corpo);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex,
                "Azure Speech indisponivel ao transcrever o segmento {SegmentoId}.", req.SegmentoId);

            return Falha(req, MotivoFalhaTranscricao.MotorIndisponivel);
        }
    }

    /// <summary>
    /// Traduz o resultado do Azure para o contrato do callback.
    ///
    /// <c>NoMatch</c>, <c>InitialSilenceTimeout</c> e <c>BabbleTimeout</c> são áudio
    /// ilegível, e não motor fora do ar: reenviar o mesmo trecho de silêncio três vezes
    /// só gastaria quota para chegar ao mesmo lugar. O rascunho sai sem ele, com aviso.
    /// </summary>
    private CallbackDeTranscricaoDto Traduzir(SolicitarTranscricaoRequest req, RespostaDoAzureSpeech? corpo)
    {
        var melhor = corpo?.NBest?.FirstOrDefault();

        // No formato detailed o texto vem em NBest[0].Display; DisplayText so aparece no
        // formato simple — ler os dois deixa o adaptador indiferente a essa escolha.
        var texto = Preenchido(corpo?.DisplayText) ?? Preenchido(melhor?.Display) ?? Preenchido(melhor?.Lexical);

        var reconhecido =
            string.Equals(corpo?.RecognitionStatus, "Success", StringComparison.OrdinalIgnoreCase) &&
            texto is not null;

        if (!reconhecido)
        {
            _logger.LogInformation(
                "Azure Speech nao reconheceu fala no segmento {SegmentoId} (status {Status}).",
                req.SegmentoId, corpo?.RecognitionStatus ?? "desconhecido");

            return Falha(req, MotivoFalhaTranscricao.AudioIlegivel);
        }

        return new CallbackDeTranscricaoDto
        {
            SegmentoId = req.SegmentoId,
            ConsultaId = req.ConsultaId,
            Status = "Ok",
            Texto = texto,
            Confianca = melhor?.Confidence,
            Idioma = req.Idioma,
            Motor = MotorDaVez(),
            CallbackToken = req.CallbackToken
        };
    }

    /// <summary>
    /// Traduz o status HTTP do Azure para o motivo que o veterinário vê no aviso.
    ///
    /// 401 e 403 saem em nível de erro: é credencial errada, não áudio ruim — e a
    /// diferença é entre "esta consulta perdeu um trecho" e "nenhuma consulta vai
    /// transcrever até alguém arrumar a chave".
    /// </summary>
    private CallbackDeTranscricaoDto FalhaDeHttp(SolicitarTranscricaoRequest req, HttpStatusCode status)
    {
        switch (status)
        {
            case HttpStatusCode.BadRequest:
                _logger.LogWarning(
                    "Azure Speech recusou o formato do segmento {SegmentoId} (400).", req.SegmentoId);

                return Falha(req, MotivoFalhaTranscricao.FormatoNaoSuportado);

            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                _logger.LogError(
                    "Azure Speech recusou a credencial ({Status}) no segmento {SegmentoId}. " +
                    "Confira AZURE_SPEECH_KEY e a regiao configurada — nenhuma consulta transcreve assim.",
                    (int)status, req.SegmentoId);

                return Falha(req, MotivoFalhaTranscricao.MotorIndisponivel);

            case HttpStatusCode.TooManyRequests:
                _logger.LogWarning(
                    "Azure Speech limitou a taxa (429) no segmento {SegmentoId}; o backoff retenta.",
                    req.SegmentoId);

                return Falha(req, MotivoFalhaTranscricao.MotorIndisponivel);

            default:
                _logger.LogWarning(
                    "Azure Speech respondeu {Status} no segmento {SegmentoId}.", (int)status, req.SegmentoId);

                return Falha(req, MotivoFalhaTranscricao.MotorIndisponivel);
        }
    }

    private static CallbackDeTranscricaoDto Falha(SolicitarTranscricaoRequest req, MotivoFalhaTranscricao motivo) =>
        new()
        {
            SegmentoId = req.SegmentoId,
            ConsultaId = req.ConsultaId,
            Status = "Falha",
            Motivo = motivo,
            Idioma = req.Idioma,
            Motor = MotorDaVez(),
            CallbackToken = req.CallbackToken
        };

    private static MotorDeTranscricaoDto MotorDaVez() => new()
    {
        Nome = ConfiguracaoDoAzureSpeech.NomeDoMotor,
        Versao = ConfiguracaoDoAzureSpeech.VersaoDaApi
    };

    /// <summary>Formato do segmento pronto para a busca na tabela, sem espaços nas pontas.</summary>
    private static string NormalizarFormato(string? formato) =>
        string.IsNullOrWhiteSpace(formato) ? string.Empty : formato.Trim();

    private static string? Preenchido(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;
}

/// <summary>Leitura tolerante do corpo do Azure — resposta vazia não é exceção, é falha.</summary>
internal static class RespostaDoAzureExtensions
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<RespostaDoAzureSpeech?> ReadFromJsonSafeAsync(
        this HttpContent content, CancellationToken cancellationToken)
    {
        var texto = await content.ReadAsStringAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(texto)
            ? null
            : JsonSerializer.Deserialize<RespostaDoAzureSpeech>(texto, Json);
    }
}
