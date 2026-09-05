using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vetly.Application.DTOs.Captura;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Infrastructure.Jobs;

/// <summary>
/// Despacha um segmento de áudio ao motor de transcrição (RN-009, §5.3).
///
/// Roda fora da requisição de propósito: o veterinário não pode ficar esperando a
/// transcrição para continuar o atendimento. Motor fora do ar não derruba a consulta
/// — o job falha, é retentado com espera crescente e, esgotado, o segmento é dado
/// como perdido e o rascunho sai sem ele, com aviso.
/// </summary>
public class TranscreverSegmentoHandler : IJobHandler
{
    private readonly ICapturaRepository _captura;
    private readonly IMidiaRepository _midias;
    private readonly IStorageAdapter _storage;
    private readonly ISttAdapter _stt;
    private readonly IConfiguration _config;
    private readonly ILogger<TranscreverSegmentoHandler> _logger;

    /// <summary>Validade da URL de leitura usada para buscar o áudio do segmento (§5.3).</summary>
    private static readonly TimeSpan ValidadeDaUrlDeAudio = TimeSpan.FromMinutes(15);

    public TranscreverSegmentoHandler(
        ICapturaRepository captura,
        IMidiaRepository midias,
        IStorageAdapter storage,
        ISttAdapter stt,
        IConfiguration config,
        ILogger<TranscreverSegmentoHandler> logger)
    {
        _captura = captura;
        _midias = midias;
        _storage = storage;
        _stt = stt;
        _config = config;
        _logger = logger;
    }

    /// <inheritdoc/>
    public TipoJob Tipo => TipoJob.TranscreverSegmento;

    /// <inheritdoc/>
    public async Task ExecutarAsync(Job job, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(job.Payload, out var segmentoId))
            throw new InvalidOperationException("Payload do job nao contem um id de segmento valido.");

        var segmento = await _captura.ObterSegmentoAsync(segmentoId)
            ?? throw new InvalidOperationException($"Segmento {segmentoId} nao encontrado.");

        // O callback pode ter chegado antes deste job rodar — nada a fazer
        if (segmento.TemDesfecho())
            return;

        var midia = await _midias.ObterPorIdAsync(segmento.MidiaId)
            ?? throw new InvalidOperationException($"Midia {segmento.MidiaId} nao encontrada.");

        var sessao = await _captura.ObterSessaoAsync(segmento.SessaoCapturaId)
            ?? throw new InvalidOperationException("Sessao de captura nao encontrada.");

        // URL temporaria de leitura: e por ela que o adaptador alcanca o audio no storage
        var audio = await _storage.GerarUrlDeLeituraAsync(midia.ChaveStorage, ValidadeDaUrlDeAudio);

        // A URL precisa ser absoluta porque alguem vai busca-la: com a Fast Transcription
        // do Azure quem baixa e a propria API — o audio vai inline na chamada, e o motor
        // nao precisa mais alcancar o nosso storage. A exigencia, portanto, encolheu: o
        // endereco tem de ser resolvivel POR NOS, e nao mais publicamente por terceiros.
        // Ainda assim se confere aqui, que e o ultimo ponto antes de o endereco sair
        // deste handler: caminho relativo nao resolve nem para a propria API, e o
        // segmento morreria sem motivo aproveitavel no diagnostico. Outros adaptadores
        // (Node-RED, motores que buscam o audio) continuam dependendo de a URL ser
        // alcancavel de fora — e e Storage:PublicBaseUrl que decide isso.
        if (!Uri.TryCreate(audio.Url, UriKind.Absolute, out _))
        {
            segmento.RegistrarFalha(MotivoFalhaTranscricao.MotorIndisponivel);
            _captura.AtualizarSegmento(segmento);
            await _captura.SalvarAsync();

            _logger.LogError(
                "URL de audio do segmento {SegmentoId} nao e absoluta ({Url}). " +
                "Configure Storage:PublicBaseUrl.", segmento.Id, audio.Url);

            throw new InvalidOperationException(
                $"A URL de audio do segmento {segmento.Id} nao e absoluta e nao seria possivel baixa-la.");
        }

        // O token amarra o callback ao segmento; a base guarda so o hash dele
        var token = GerarToken(segmento.Id);

        var aceito = await _stt.SolicitarTranscricaoAsync(new SolicitarTranscricaoRequest(
            segmento.Id,
            sessao.ConsultaId,
            segmento.Sequencia,
            audio.Url,
            midia.ContentType,
            "pt-BR",
            $"{_config["Servicos:CallbackBaseUrl"] ?? string.Empty}/api/internos/stt/callback",
            token));

        if (!aceito)
        {
            segmento.RegistrarFalha(MotivoFalhaTranscricao.MotorIndisponivel);
            _captura.AtualizarSegmento(segmento);
            await _captura.SalvarAsync();

            throw new InvalidOperationException("O motor de transcricao nao aceitou o segmento.");
        }

        segmento.RegistrarDespacho(HashDoToken(token), DateTime.UtcNow);
        _captura.AtualizarSegmento(segmento);
        await _captura.SalvarAsync();

        _logger.LogInformation(
            "Segmento {SegmentoId} (trecho {Sequencia}) despachado para transcricao.",
            segmento.Id, segmento.Sequencia);
    }

    /// <summary>Token derivado do segmento, para o motor devolver no callback.</summary>
    private static string GerarToken(Guid segmentoId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{segmentoId}|{Guid.NewGuid()}")))
            .ToLowerInvariant()[..32];

    private static string HashDoToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

/// <summary>
/// Entrega a transcrição do motor simulado pelo mesmo caminho que o fluxo Node-RED
/// usaria (§5.3).
///
/// Sem isso, a transcrição simulada apareceria dentro da própria requisição e o fluxo
/// assíncrono — que é o que precisa funcionar quando o motor real entrar — nunca
/// seria exercitado.
/// </summary>
public class TranscreverSegmentoSimuladoHandler : IJobHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ICapturaService _captura;
    private readonly ILogger<TranscreverSegmentoSimuladoHandler> _logger;

    public TranscreverSegmentoSimuladoHandler(
        ICapturaService captura, ILogger<TranscreverSegmentoSimuladoHandler> logger)
    {
        _captura = captura;
        _logger = logger;
    }

    /// <inheritdoc/>
    public TipoJob Tipo => TipoJob.TranscreverSegmentoSimulado;

    /// <inheritdoc/>
    public async Task ExecutarAsync(Job job, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(job.Payload))
            throw new InvalidOperationException("Payload da transcricao simulada esta vazio.");

        var callback = JsonSerializer.Deserialize<CallbackDeTranscricaoDto>(job.Payload, Json)
            ?? throw new InvalidOperationException("Payload da transcricao simulada e invalido.");

        await _captura.RegistrarCallbackAsync(callback);

        _logger.LogInformation(
            "Transcricao simulada entregue para o segmento {SegmentoId}.", callback.SegmentoId);
    }
}

/// <summary>
/// Resolve os segmentos que saíram para o motor e cujo callback nunca voltou (§4.2).
///
/// O despacho aceito não é garantia de resposta: motor que aceita o trabalho e depois
/// morre calado deixa o segmento em <c>Enviado</c> para sempre. Como o desfecho da
/// sessão só é avaliado quando <b>todos</b> os trechos responderam, um único segmento
/// pendurado prende a consulta inteira em <c>AguardandoTranscricao</c> — e o app, que
/// faz polling do rascunho, nunca vê um estado terminal.
///
/// O trabalho de fato está no <c>ICapturaService</c>: é lá que vive a máquina de
/// estados da sessão, e duplicá-la aqui daria dois lugares para o mesmo desfecho.
/// </summary>
public class VerificarTranscricaoTravadaHandler : IJobHandler
{
    private readonly ICapturaService _captura;
    private readonly ILogger<VerificarTranscricaoTravadaHandler> _logger;

    public VerificarTranscricaoTravadaHandler(
        ICapturaService captura, ILogger<VerificarTranscricaoTravadaHandler> logger)
    {
        _captura = captura;
        _logger = logger;
    }

    /// <inheritdoc/>
    public TipoJob Tipo => TipoJob.VerificarTranscricaoTravada;

    /// <inheritdoc/>
    public async Task ExecutarAsync(Job job, CancellationToken cancellationToken)
    {
        var tratados = await _captura.ResolverSegmentosTravadosAsync();

        if (tratados > 0)
        {
            _logger.LogWarning(
                "{Quantidade} segmento(s) sem callback dentro do prazo foram tratados como travados.",
                tratados);
        }
    }
}

/// <summary>
/// Estrutura a transcrição da consulta em prontuário pela IA (RN-080, §7.3).
///
/// Roda fora da requisição porque a estruturação é lenta e o veterinário já encerrou
/// o atendimento. Falha da IA não trava a consulta: a sessão cai no caminho manual e
/// o job é retentado — se nem assim der certo, o prontuário é preenchido à mão
/// (RN-085).
/// </summary>
public class EstruturarConsultaHandler : IJobHandler
{
    private readonly IRascunhoService _rascunhos;
    private readonly ILogger<EstruturarConsultaHandler> _logger;

    public EstruturarConsultaHandler(
        IRascunhoService rascunhos, ILogger<EstruturarConsultaHandler> logger)
    {
        _rascunhos = rascunhos;
        _logger = logger;
    }

    /// <inheritdoc/>
    public TipoJob Tipo => TipoJob.EstruturarConsulta;

    /// <inheritdoc/>
    public async Task ExecutarAsync(Job job, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(job.Payload, out var sessaoId))
            throw new InvalidOperationException("Payload do job nao contem um id de sessao valido.");

        await _rascunhos.GerarAsync(sessaoId);

        _logger.LogInformation("Estruturacao concluida para a sessao {SessaoId}.", sessaoId);
    }
}
