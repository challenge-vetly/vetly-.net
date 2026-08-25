using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vetly.Application.Interfaces;

namespace Vetly.API.HealthChecks;

/// <summary>
/// Verifica a disponibilidade do Ollama (LLM local), o unico servico externo do qual
/// a API depende. O Ollama expoe na raiz (<c>GET /</c>) um endpoint de status que
/// responde 200 com "Ollama is running" quando o daemon esta ativo.
/// </summary>
/// <remarks>
/// Falha aqui e reportada como <see cref="HealthStatus.Degraded"/>, nao Unhealthy:
/// sem o Ollama apenas os recursos de IA (sugestao de diagnostico, triagem) param —
/// o restante da API continua atendendo normalmente.
/// </remarks>
public sealed class OllamaHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Resolve o mesmo <see cref="HttpClient"/> nomeado que o <c>OllamaService</c> usa,
    /// para que o check herde BaseAddress e timeout ja configurados no <c>Program.cs</c>.
    /// </summary>
    /// <remarks>
    /// O nome do client tipado registrado por <c>AddHttpClient&lt;IOllamaService, OllamaService&gt;</c>
    /// deriva do tipo do *contrato* (TClient), por isso <c>nameof(IOllamaService)</c>.
    /// </remarks>
    public OllamaHealthCheck(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(IOllamaService));
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Timeout curto e proprio: o client compartilhado usa 120s (adequado para
            // inferencia, nao para uma sonda). Um health check nao pode travar o probe.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            var resposta = await _httpClient.GetAsync("/", timeout.Token);

            return resposta.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"Ollama respondeu {(int)resposta.StatusCode} em {_httpClient.BaseAddress}.")
                : HealthCheckResult.Degraded($"Ollama respondeu {(int)resposta.StatusCode}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Estourou os 5s locais — o daemon esta lento ou pendurado.
            return HealthCheckResult.Degraded("Ollama nao respondeu em 5s.");
        }
        catch (Exception ex)
        {
            // Conexao recusada, DNS invalido, etc. Recursos de IA indisponiveis.
            return HealthCheckResult.Degraded($"Ollama inacessivel: {ex.Message}", ex);
        }
    }
}
