using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vetly.Application.Interfaces;

namespace Vetly.Infrastructure.Adapters;

/// <summary>
/// Transcrição pelo fluxo Node-RED (§5.3).
///
/// Ida: <c>POST {NODE_RED_URL}/vetly/stt</c> com o áudio por URL temporária; o fluxo
/// responde 202 e processa em segundo plano. Volta: o fluxo chama
/// <c>POST /api/internos/stt/callback</c> com o texto.
///
/// O contrato do callback é da Vetly, não do motor — trocar o motor é mexer dentro do
/// fluxo Node-RED, sem tocar aqui.
/// </summary>
public class SttAdapterNodeRed : ISttAdapter
{
    private readonly HttpClient _http;
    private readonly ILogger<SttAdapterNodeRed> _logger;

    public SttAdapterNodeRed(HttpClient http, ILogger<SttAdapterNodeRed> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> SolicitarTranscricaoAsync(SolicitarTranscricaoRequest req)
    {
        try
        {
            var resposta = await _http.PostAsJsonAsync("/vetly/stt", new
            {
                segmentoId = req.SegmentoId,
                consultaId = req.ConsultaId,
                sequencia = req.Sequencia,
                audioUrl = req.AudioUrl,
                formato = req.Formato,
                idioma = req.Idioma,
                callbackUrl = req.CallbackUrl,
                callbackToken = req.CallbackToken
            });

            if (!resposta.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Node-RED recusou o segmento {SegmentoId} com status {Status}.",
                    req.SegmentoId, (int)resposta.StatusCode);

                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Motor fora do ar nao pode derrubar a consulta: o segmento volta para a
            // fila e o worker retenta com espera crescente (§4.2).
            _logger.LogWarning(ex, "Node-RED indisponivel ao despachar o segmento {SegmentoId}.", req.SegmentoId);
            return false;
        }
    }
}

/// <summary>
/// Transcrição simulada, para desenvolvimento sem Node-RED (§5.3).
///
/// Devolve o texto pelo mesmo callback que o fluxo real usaria, com um pequeno
/// atraso — assim o fluxo assíncrono é exercitado de verdade, em vez de a
/// transcrição aparecer dentro da própria requisição.
///
/// O texto é sintético e marcado como tal: nunca deve ser confundido com fala real
/// numa demonstração.
/// </summary>
public class SttAdapterSimulado : ISttAdapter
{
    private readonly IFilaDeJobs _fila;
    private readonly ILogger<SttAdapterSimulado> _logger;

    /// <summary>Atraso com que o motor simulado devolve o texto.</summary>
    public static readonly TimeSpan AtrasoDaTranscricao = TimeSpan.FromSeconds(3);

    public SttAdapterSimulado(IFilaDeJobs fila, ILogger<SttAdapterSimulado> logger)
    {
        _fila = fila;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> SolicitarTranscricaoAsync(SolicitarTranscricaoRequest req)
    {
        var callback = new
        {
            segmentoId = req.SegmentoId,
            consultaId = req.ConsultaId,
            status = "Ok",
            texto = TextoSintetico(req.Sequencia),
            confianca = 0.92m,
            idioma = req.Idioma,
            motor = new { nome = "stt-simulado", versao = "1.0.0" }
        };

        await _fila.EnfileirarAsync(
            Domain.Enums.TipoJob.TranscreverSegmentoSimulado,
            JsonSerializer.Serialize(callback),
            AtrasoDaTranscricao);

        _logger.LogInformation(
            "Transcricao simulada agendada para o segmento {SegmentoId}.", req.SegmentoId);

        return true;
    }

    /// <summary>
    /// Texto sintético, explicitamente marcado. Varia com a sequência para que a
    /// junção dos segmentos seja verificável.
    /// </summary>
    private static string TextoSintetico(int sequencia) =>
        $"[transcricao simulada - trecho {sequencia}] Conteudo de teste gerado pelo motor simulado, sem fala real.";
}
