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
    /// Tipos MIME que a Fast Transcription API aceita, mapeados para o cabeçalho que
    /// acompanha a parte de áudio do multipart.
    ///
    /// A lista continua <b>fechada</b>, e o motivo de ela existir é histórico e vale
    /// guardar: na REST API de short audio, que este handler usava antes, um WebM/Opus
    /// válido voltava com <b>HTTP 200 e <c>RecognitionStatus: "Success"</c> com texto
    /// vazio</b> — o Content-Type declarado não mudava nada, porque o Azure inspeciona o
    /// container. Formato não suportado não falhava: ele <b>emudecia</b>, e o
    /// veterinário lia "áudio ilegível" quando o problema era o container.
    ///
    /// A Fast Transcription aceita a lista abaixo nativamente, então a guarda recusa
    /// muito menos coisa do que recusava — mas segue sendo o lugar que dá o motivo certo
    /// (<c>FormatoNaoSuportado</c>) sem gastar chamada e retentativas para chegar nele.
    /// </summary>
    private static readonly Dictionary<string, string> ContentTypeDoAzure = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/webm"] = "audio/webm",
        ["audio/webm;codecs=opus"] = "audio/webm",
        ["audio/webm; codecs=opus"] = "audio/webm",
        ["audio/ogg"] = "audio/ogg",
        ["audio/ogg;codecs=opus"] = "audio/ogg",
        ["audio/ogg; codecs=opus"] = "audio/ogg",
        ["audio/wav"] = "audio/wav",
        ["audio/wave"] = "audio/wav",
        ["audio/x-wav"] = "audio/wav",
        ["audio/mpeg"] = "audio/mpeg",
        ["audio/flac"] = "audio/flac",
        ["audio/amr"] = "audio/amr"
    };

    /// <summary>
    /// Configuração de reconhecimento que viaja na parte <c>definition</c>.
    ///
    /// <c>profanityFilterMode: None</c> e não <c>Masked</c>: isto é prontuário clínico,
    /// não conteúdo público. Mascarar palavra faria o texto do atendimento chegar ao
    /// veterinário com asteriscos no lugar do que foi dito — e o que ele precisa é do
    /// registro fiel.
    /// </summary>
    private const string DefinicaoDeReconhecimento =
        """{"locales":["pt-BR"],"profanityFilterMode":"None"}""";

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
                "Segmento {SegmentoId} veio em '{Formato}', que a Fast Transcription API nao aceita. " +
                "Formatos aceitos: WebM, OGG, WAV, MP3, FLAC e AMR.", req.SegmentoId, req.Formato);

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
            // A Fast Transcription recebe o audio inline, em multipart: uma parte com os
            // bytes e outra com a configuracao de reconhecimento. E o que dispensa a URL
            // publica que a API de short audio exigia — o Azure nao precisa mais alcancar
            // o nosso storage.
            using var conteudo = new MultipartFormDataContent();

            var parteDoAudio = new ByteArrayContent(audio);
            parteDoAudio.Headers.TryAddWithoutValidation("Content-Type", contentType);
            conteudo.Add(parteDoAudio, "audio", $"segmento-{req.Sequencia}");

            var parteDaDefinicao = new StringContent(DefinicaoDeReconhecimento);
            parteDaDefinicao.Headers.TryAddWithoutValidation("Content-Type", "application/json");
            conteudo.Add(parteDaDefinicao, "definition");

            var cliente = _clientes.CreateClient(ConfiguracaoDoAzureSpeech.NomeDoHttpClient);

            using var resposta = await cliente.PostAsync(
                _azure.CaminhoDeTranscricao(), conteudo, cancellationToken);

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
    /// Resposta sem <c>combinedPhrases</c> — ou com texto vazio nela — é áudio ilegível,
    /// e não motor fora do ar: é o mesmo desfecho que o <c>NoMatch</c> da API anterior
    /// tinha, pelo mesmo motivo. Reenviar o mesmo trecho de silêncio três vezes só
    /// gastaria quota para chegar ao mesmo lugar; o rascunho sai sem ele, com aviso.
    /// </summary>
    private CallbackDeTranscricaoDto Traduzir(SolicitarTranscricaoRequest req, RespostaDoAzureSpeech? corpo)
    {
        var texto = Preenchido(corpo?.CombinedPhrases?.FirstOrDefault()?.Text);

        if (texto is null)
        {
            _logger.LogInformation(
                "Azure Speech nao reconheceu fala no segmento {SegmentoId}.", req.SegmentoId);

            return Falha(req, MotivoFalhaTranscricao.AudioIlegivel);
        }

        var frases = corpo?.Phrases ?? [];

        return new CallbackDeTranscricaoDto
        {
            SegmentoId = req.SegmentoId,
            ConsultaId = req.ConsultaId,
            Status = "Ok",
            Texto = texto,
            Confianca = ConfiancaDe(frases),

            // Os trechos com marcacao de tempo sao o que permite conferir cada frase do
            // rascunho contra o audio (§5.3). A API de short audio nao os devolvia e o
            // campo ficava sempre nulo; a Fast Transcription os traz, e serializar as
            // frases como vieram preserva offset e duracao sem inventar formato proprio.
            Trechos = frases.Count > 0 ? JsonSerializer.Serialize(frases, Json) : null,

            Idioma = req.Idioma,
            Motor = MotorDaVez(),
            CallbackToken = req.CallbackToken
        };
    }

    /// <summary>
    /// Confiança do segmento: média das frases, ou a da primeira quando só ela tem valor.
    ///
    /// A média é o que representa o trecho inteiro — uma frase muito curta e muito
    /// confiante não deveria responder por trinta segundos de áudio. Frases sem
    /// confiança ficam fora do cálculo em vez de entrarem como zero, que puxaria a média
    /// para baixo por ausência de dado, não por incerteza do motor.
    /// </summary>
    private static decimal? ConfiancaDe(List<FraseDoAzure> frases)
    {
        var medidas = frases.Where(f => f.Confidence is not null).Select(f => f.Confidence!.Value).ToList();

        return medidas.Count > 0 ? Math.Round(medidas.Average(), 4) : null;
    }

    /// <summary>
    /// Traduz o status HTTP do Azure para o motivo que o veterinário vê no aviso.
    ///
    /// 401 e 403 saem em nível de erro: é credencial errada, não áudio ruim — e a
    /// diferença é entre "esta consulta perdeu um trecho" e "nenhuma consulta vai
    /// transcrever até alguém arrumar a chave".
    ///
    /// A divisão que importa é entre <b>retentável</b> e <b>não retentável</b>, e a
    /// documentação da Microsoft é explícita sobre ela: 429 e 5xx são transitórios e
    /// vale reenviar com espera; 400, 401, 403 e 422 são erro de cliente, e reenviar o
    /// mesmo pedido produz o mesmo erro. Nenhum dos dois retenta para sempre — o teto de
    /// <see cref="SegmentoAudio.MaximoDeTentativas"/> vale para todos —, mas gastar as
    /// três tentativas num pedido que já se sabe malformado só atrasa o aviso ao
    /// veterinário.
    /// </summary>
    private CallbackDeTranscricaoDto FalhaDeHttp(SolicitarTranscricaoRequest req, HttpStatusCode status)
    {
        switch (status)
        {
            // 422 acompanha o 400: nos dois o Azure diz que o pedido esta malformado, e
            // o que o veterinario precisa saber e que o audio nao serviu.
            case HttpStatusCode.BadRequest:
            case HttpStatusCode.UnprocessableEntity:
                _logger.LogWarning(
                    "Azure Speech recusou o pedido do segmento {SegmentoId} ({Status}) — erro de cliente, " +
                    "reenviar o mesmo audio daria o mesmo resultado.", req.SegmentoId, (int)status);

                return Falha(req, MotivoFalhaTranscricao.FormatoNaoSuportado);

            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                _logger.LogError(
                    "Azure Speech recusou a credencial ({Status}) no segmento {SegmentoId}. " +
                    "Confira AZURE_SPEECH_KEY e o endpoint configurado — nenhuma consulta transcreve assim.",
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

    private CallbackDeTranscricaoDto Falha(SolicitarTranscricaoRequest req, MotivoFalhaTranscricao motivo) =>
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

    /// <summary>
    /// Motor e versão gravados na trilha da transcrição.
    ///
    /// A versão sai da configuração, e não de uma constante: subir a API por variável de
    /// ambiente sem que a auditoria acompanhe deixaria o registro dizendo uma coisa e o
    /// serviço fazendo outra.
    /// </summary>
    private MotorDeTranscricaoDto MotorDaVez() => new()
    {
        Nome = ConfiguracaoDoAzureSpeech.NomeDoMotor,
        Versao = _azure.VersaoDaApi
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
