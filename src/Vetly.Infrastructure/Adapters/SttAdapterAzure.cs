using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.Adapters;

/// <summary>
/// Endereço e credencial do Azure Speech (§5.3).
///
/// A chave vem <b>só</b> da variável de ambiente <c>AZURE_SPEECH_KEY</c>: em
/// <c>appsettings.json</c> ela iria para o repositório na primeira distração. A região
/// aceita as duas origens porque não é segredo — é topologia.
///
/// O endpoint é montado a partir da região, e não configurado inteiro: um endereço
/// digitado à mão é a forma mais fácil de apontar para a região errada e descobrir
/// pelo 401.
/// </summary>
public sealed class ConfiguracaoDoAzureSpeech
{
    /// <summary>Nome do <c>HttpClient</c> registrado para falar com o Azure.</summary>
    public const string NomeDoHttpClient = "azure-speech";

    /// <summary>Versão da REST API de reconhecimento de fala curta.</summary>
    public const string VersaoDaApi = "v1";

    /// <summary>Nome do motor gravado na trilha de auditoria da transcrição.</summary>
    public const string NomeDoMotor = "azure-speech";

    /// <summary>Região do recurso de Speech (ex.: <c>canadacentral</c>).</summary>
    public string Regiao { get; }

    /// <summary>Chave de assinatura, enviada no header <c>Ocp-Apim-Subscription-Key</c>.</summary>
    public string Chave { get; }

    public ConfiguracaoDoAzureSpeech(IConfiguration config)
    {
        Regiao = Preenchido(config["AZURE_SPEECH_REGION"]) ?? Preenchido(config["Azure:Speech:Region"])
            ?? throw new InvalidOperationException(
                "Regiao do Azure Speech nao configurada. Defina AZURE_SPEECH_REGION ou Azure:Speech:Region.");

        Chave = Preenchido(config["AZURE_SPEECH_KEY"])
            ?? throw new InvalidOperationException(
                "AZURE_SPEECH_KEY nao definida. A chave vem de variavel de ambiente, nunca do appsettings.");
    }

    /// <summary>
    /// Host de reconhecimento da região. É o endpoint <c>*.stt.speech.microsoft.com</c>,
    /// e não o <c>*.api.cognitive.microsoft.com</c> genérico de Cognitive Services —
    /// o genérico existe para emissão de token e responde 404 ao reconhecimento.
    /// </summary>
    public Uri BaseUrl => new($"https://{Regiao}.stt.speech.microsoft.com/");

    /// <summary>Caminho do reconhecimento de fala curta, com o idioma esperado.</summary>
    public string CaminhoDeReconhecimento(string idioma) =>
        $"speech/recognition/conversation/cognitiveservices/{VersaoDaApi}" +
        $"?language={Uri.EscapeDataString(idioma)}&format=detailed";

    private static string? Preenchido(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;
}

/// <summary>
/// Transcrição pelo Azure Speech, sem intermediário (§5.3).
///
/// O adaptador <b>não fala com o Azure</b>: ele só enfileira o trabalho e devolve
/// "aceito". A chamada de rede acontece no <see cref="TranscreverSegmentoAzureHandler"/>,
/// fora da requisição — <c>SolicitarTranscricaoAsync</c> tem semântica de "aceito,
/// processo depois", e transcrever aqui prenderia o veterinário esperando o Azure
/// responder para poder continuar o atendimento.
///
/// O contrato do callback continua sendo o da Vetly: o handler devolve o texto pelo
/// mesmo <c>RegistrarCallbackAsync</c> que o fluxo por HTTP usaria.
/// </summary>
public class SttAdapterAzure : ISttAdapter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IFilaDeJobs _fila;
    private readonly ILogger<SttAdapterAzure> _logger;

    public SttAdapterAzure(IFilaDeJobs fila, ILogger<SttAdapterAzure> logger)
    {
        _fila = fila;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> SolicitarTranscricaoAsync(SolicitarTranscricaoRequest req)
    {
        try
        {
            await _fila.EnfileirarAsync(
                TipoJob.TranscreverSegmentoAzure, JsonSerializer.Serialize(req, Json));

            _logger.LogInformation(
                "Segmento {SegmentoId} (trecho {Sequencia}) enfileirado para o Azure Speech.",
                req.SegmentoId, req.Sequencia);

            return true;
        }
        catch (Exception ex)
        {
            // Nao conseguir enfileirar e falha de infraestrutura da propria Vetly, e nao
            // do motor: devolver false faz o job de despacho falhar e ser retentado com
            // espera crescente, em vez de dar o segmento como perdido aqui.
            _logger.LogError(ex,
                "Nao foi possivel enfileirar o segmento {SegmentoId} para o Azure Speech.", req.SegmentoId);

            return false;
        }
    }
}

/// <summary>Resposta do reconhecimento de fala curta do Azure, formato <c>detailed</c>.</summary>
internal sealed class RespostaDoAzureSpeech
{
    [JsonPropertyName("RecognitionStatus")]
    public string? RecognitionStatus { get; set; }

    [JsonPropertyName("DisplayText")]
    public string? DisplayText { get; set; }

    [JsonPropertyName("NBest")]
    public List<HipoteseDoAzureSpeech>? NBest { get; set; }
}

/// <summary>Uma hipótese de reconhecimento na lista <c>NBest</c>.</summary>
internal sealed class HipoteseDoAzureSpeech
{
    [JsonPropertyName("Confidence")]
    public decimal? Confidence { get; set; }

    [JsonPropertyName("Display")]
    public string? Display { get; set; }

    [JsonPropertyName("Lexical")]
    public string? Lexical { get; set; }
}
