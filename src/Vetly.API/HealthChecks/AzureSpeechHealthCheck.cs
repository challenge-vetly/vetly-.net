using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vetly.Infrastructure.Adapters;

namespace Vetly.API.HealthChecks;

/// <summary>
/// Verifica a disponibilidade do Azure Speech, o motor de transcricao da consulta
/// quando <c>Adaptadores:Stt = Azure</c>.
///
/// A sonda e um GET na listagem de transcricoes, e nao uma transcricao de verdade: um
/// POST de audio custaria quota a cada probe, e o que se quer saber aqui — o endpoint
/// responde e a chave e aceita — a listagem responde igual, de graca.
///
/// A sonda usa o <b>mesmo endpoint e a mesma versao de API</b> que as transcricoes.
/// Antes ela batia no <c>issueToken</c> regional, que e outro host: passava mesmo com
/// o endpoint de transcricao errado, e o health check dizia "saudavel" enquanto nenhum
/// segmento transcrevia.
/// </summary>
/// <remarks>
/// Falha aqui e reportada como <see cref="HealthStatus.Degraded"/>, nao Unhealthy, na
/// mesma severidade do <see cref="OllamaHealthCheck"/>: sem o Azure a captura de audio
/// para, mas a consulta continua acontecendo e o prontuario segue pelo caminho manual
/// (RN-085). Tirar a API inteira de rotacao por isso seria pior que a falha.
/// </remarks>
public sealed class AzureSpeechHealthCheck : IHealthCheck
{
    /// <summary>Timeout proprio da sonda. Health check que trava e pior que o que falha.</summary>
    private static readonly TimeSpan TimeoutDaSonda = TimeSpan.FromSeconds(5);

    private readonly HttpClient _httpClient;
    private readonly ConfiguracaoDoAzureSpeech _azure;

    /// <summary>
    /// Reaproveita o mesmo <see cref="HttpClient"/> nomeado que o handler de transcricao
    /// usa, para que a sonda herde a chave e o endpoint ja configurados no Program.cs — e
    /// nao teste uma configuracao diferente da que atende de verdade.
    /// </summary>
    public AzureSpeechHealthCheck(IHttpClientFactory httpClientFactory, ConfiguracaoDoAzureSpeech azure)
    {
        _httpClient = httpClientFactory.CreateClient(ConfiguracaoDoAzureSpeech.NomeDoHttpClient);
        _azure = azure;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Relativo de proposito: resolve contra a BaseAddress do cliente nomeado, que e
        // o mesmo endpoint que as transcricoes usam. Um endereco absoluto aqui poderia
        // sondar um host que a transcricao nem chama.
        var sonda = _azure.CaminhoDeListagem();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeoutDaSonda);

            using var resposta = await _httpClient.GetAsync(sonda, timeout.Token);

            if (resposta.IsSuccessStatusCode)
                return HealthCheckResult.Healthy($"Azure Speech respondeu em {_httpClient.BaseAddress}.");

            // 401/403 e credencial, nao indisponibilidade: a mensagem precisa dizer
            // isso, ou o plantao vai procurar rede onde o problema e chave.
            var detalhe = resposta.StatusCode is System.Net.HttpStatusCode.Unauthorized
                                              or System.Net.HttpStatusCode.Forbidden
                ? "chave recusada (confira AZURE_SPEECH_KEY e o endpoint)"
                : "resposta inesperada";

            return HealthCheckResult.Degraded(
                $"Azure Speech respondeu {(int)resposta.StatusCode}: {detalhe}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Degraded(
                $"Azure Speech nao respondeu em {TimeoutDaSonda.TotalSeconds:0}s.");
        }
        catch (Exception ex)
        {
            // Conexao recusada, DNS invalido, endpoint inexistente: captura indisponivel.
            return HealthCheckResult.Degraded($"Azure Speech inacessivel: {ex.Message}", ex);
        }
    }
}
