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
/// <c>appsettings.json</c> ela iria para o repositório na primeira distração. Endpoint,
/// região e versão da API aceitam as duas origens porque não são segredo — são
/// topologia.
///
/// O endpoint tem origem própria (<c>AZURE_SPEECH_ENDPOINT</c>) porque o recurso de
/// Speech pode estar num domínio customizado, e derivar tudo da região deixaria esse
/// caso sem saída. Quando ele não vem, a região ainda monta um endereço válido — é o
/// caminho de sempre, e continua sendo o que a maioria das instalações usa.
/// </summary>
public sealed class ConfiguracaoDoAzureSpeech
{
    /// <summary>Nome do <c>HttpClient</c> registrado para falar com o Azure.</summary>
    public const string NomeDoHttpClient = "azure-speech";

    /// <summary>
    /// Versão padrão da Fast Transcription API.
    ///
    /// <c>2025-10-15</c> também existe e habilita <c>phraseList</c> — vocabulário
    /// dirigido, que valeria muito para termos veterinários. Fica como configuração e
    /// não como troca de constante para que subir de versão não precise de deploy de
    /// código.
    /// </summary>
    public const string VersaoDaApiPadrao = "2024-11-15";

    /// <summary>Nome do motor gravado na trilha de auditoria da transcrição.</summary>
    public const string NomeDoMotor = "azure-speech";

    /// <summary>Região do recurso de Speech (ex.: <c>canadacentral</c>).</summary>
    public string Regiao { get; }

    /// <summary>Chave de assinatura, enviada no header <c>Ocp-Apim-Subscription-Key</c>.</summary>
    public string Chave { get; }

    /// <summary>Versão da REST API efetivamente usada nas chamadas.</summary>
    public string VersaoDaApi { get; }

    public ConfiguracaoDoAzureSpeech(IConfiguration config)
    {
        Regiao = Preenchido(config["AZURE_SPEECH_REGION"]) ?? Preenchido(config["Azure:Speech:Region"])
            ?? throw new InvalidOperationException(
                "Regiao do Azure Speech nao configurada. Defina AZURE_SPEECH_REGION ou Azure:Speech:Region.");

        Chave = Preenchido(config["AZURE_SPEECH_KEY"])
            ?? throw new InvalidOperationException(
                "AZURE_SPEECH_KEY nao definida. A chave vem de variavel de ambiente, nunca do appsettings.");

        VersaoDaApi = Preenchido(config["AZURE_SPEECH_API_VERSION"])
            ?? Preenchido(config["Azure:Speech:ApiVersion"])
            ?? VersaoDaApiPadrao;

        BaseUrl = MontarBaseUrl(
            Preenchido(config["AZURE_SPEECH_ENDPOINT"]) ?? Preenchido(config["Azure:Speech:Endpoint"]));
    }

    /// <summary>
    /// Host da Fast Transcription API.
    ///
    /// Configurado (<c>https://{recurso}.cognitiveservices.azure.com</c>) quando há
    /// endpoint próprio; derivado da região
    /// (<c>https://{regiao}.api.cognitive.microsoft.com</c>) quando não há. O host de
    /// short audio (<c>*.stt.speech.microsoft.com</c>) <b>não</b> serve aqui: a Fast
    /// Transcription mora sob Cognitive Services.
    /// </summary>
    public Uri BaseUrl { get; }

    /// <summary>
    /// Caminho da transcrição rápida.
    ///
    /// O idioma não vai mais na query: ele viaja no <c>definition</c> do multipart, com
    /// o resto da configuração de reconhecimento.
    /// </summary>
    public string CaminhoDeTranscricao() =>
        $"speechtotext/transcriptions:transcribe?api-version={Uri.EscapeDataString(VersaoDaApi)}";

    /// <summary>Caminho de listagem, usado só como sonda barata pelo health check.</summary>
    public string CaminhoDeListagem() =>
        $"speechtotext/transcriptions?api-version={Uri.EscapeDataString(VersaoDaApi)}";

    /// <summary>
    /// Endpoint configurado tem precedência; sem ele, a região monta o endereço.
    ///
    /// A barra final é forçada porque o <c>HttpClient</c> resolve caminho relativo
    /// contra a <c>BaseAddress</c>: sem ela, o último segmento do host seria descartado
    /// e a chamada iria para o lugar errado — silenciosamente.
    /// </summary>
    private Uri MontarBaseUrl(string? endpointConfigurado)
    {
        var bruto = endpointConfigurado ?? $"https://{Regiao}.api.cognitive.microsoft.com";

        if (!Uri.TryCreate(bruto, UriKind.Absolute, out var uri))
            throw new InvalidOperationException(
                $"AZURE_SPEECH_ENDPOINT '{bruto}' nao e uma URL absoluta valida.");

        return uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
    }

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

/// <summary>
/// Resposta da Fast Transcription API.
///
/// Os nomes vêm em <c>camelCase</c>, ao contrário do <c>PascalCase</c> da API de short
/// audio — daí os atributos explícitos, que deixam a diferença registrada no tipo em
/// vez de depender da convenção do serializador.
/// </summary>
internal sealed class RespostaDoAzureSpeech
{
    [JsonPropertyName("durationMilliseconds")]
    public int? DurationMilliseconds { get; set; }

    /// <summary>Texto contínuo por idioma reconhecido. É de onde sai a transcrição.</summary>
    [JsonPropertyName("combinedPhrases")]
    public List<FraseCombinadaDoAzure>? CombinedPhrases { get; set; }

    /// <summary>Frases com marcação de tempo — o que alimenta os trechos do callback.</summary>
    [JsonPropertyName("phrases")]
    public List<FraseDoAzure>? Phrases { get; set; }
}

/// <summary>O texto inteiro de um idioma, já concatenado pelo Azure.</summary>
internal sealed class FraseCombinadaDoAzure
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>Uma frase reconhecida, com onde começa, quanto dura e o quanto o motor confia.</summary>
internal sealed class FraseDoAzure
{
    [JsonPropertyName("offsetMilliseconds")]
    public int? OffsetMilliseconds { get; set; }

    [JsonPropertyName("durationMilliseconds")]
    public int? DurationMilliseconds { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("confidence")]
    public decimal? Confidence { get; set; }

    [JsonPropertyName("locale")]
    public string? Locale { get; set; }
}
