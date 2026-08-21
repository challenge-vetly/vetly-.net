using Microsoft.Extensions.Diagnostics.HealthChecks;
using Vetly.Application.Interfaces;

namespace Vetly.API.HealthChecks;

public class OllamaHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;

    public OllamaHealthCheck(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient(nameof(IOllamaService));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Ollama está respondendo.")
                : HealthCheckResult.Degraded($"Ollama retornou {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"Ollama não está respondendo: {ex.Message}");
        }
    }
}