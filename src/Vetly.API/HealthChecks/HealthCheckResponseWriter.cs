using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Vetly.API.HealthChecks;

/// <summary>
/// Serializa o <see cref="HealthReport"/> produzido pelo pipeline de health checks
/// em um JSON legível, no lugar da resposta padrao do ASP.NET Core (texto puro
/// "Healthy" / "Degraded" / "Unhealthy", que nao diz *qual* dependencia falhou).
/// </summary>
public static class HealthCheckResponseWriter
{
    /// <summary>Opcoes de serializacao reutilizadas — evita realocar a cada requisicao.</summary>
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Cria o delegate de escrita da resposta usado por <c>HealthCheckOptions.ResponseWriter</c>.
    /// </summary>
    /// <param name="incluirDetalhesDeErro">
    /// Quando <c>true</c>, inclui a mensagem da excecao que derrubou cada check.
    /// Deve ser habilitado apenas fora de Producao: mensagens de erro do Oracle expoem
    /// host, porta e codigo ORA-, o que e informacao sensivel em um endpoint publico.
    /// </param>
    public static Func<HttpContext, HealthReport, Task> Create(bool incluirDetalhesDeErro)
    {
        return (context, report) =>
        {
            // Informa ao cliente que o corpo e JSON em UTF-8.
            context.Response.ContentType = "application/json; charset=utf-8";

            // O status HTTP em si (200 para Healthy/Degraded, 503 para Unhealthy)
            // ja foi definido pelo middleware antes deste writer ser chamado.
            var payload = new
            {
                status = report.Status.ToString(),
                duracaoTotalMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
                checks = report.Entries.Select(entrada => new
                {
                    nome = entrada.Key,
                    status = entrada.Value.Status.ToString(),
                    descricao = entrada.Value.Description,
                    duracaoMs = Math.Round(entrada.Value.Duration.TotalMilliseconds, 2),
                    tags = entrada.Value.Tags,
                    erro = incluirDetalhesDeErro ? entrada.Value.Exception?.Message : null
                })
            };

            return context.Response.WriteAsync(JsonSerializer.Serialize(payload, _jsonOptions));
        };
    }
}
