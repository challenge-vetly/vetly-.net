namespace Vetly.API.Middlewares;

/// <summary>
/// Middleware de log de requisicoes HTTP.
/// Registra metodo, path, status e duracao de cada request com correlationId.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.TraceIdentifier;
        var inicio = DateTime.UtcNow;

        _logger.LogInformation(
            "CorrelationId={CorrelationId} | --> {Method} {Path}",
            correlationId, context.Request.Method, context.Request.Path);

        await _next(context);

        var duracao = (DateTime.UtcNow - inicio).TotalMilliseconds;
        _logger.LogInformation(
            "CorrelationId={CorrelationId} | <-- {StatusCode} {Method} {Path} ({DuracaoMs}ms)",
            correlationId, context.Response.StatusCode,
            context.Request.Method, context.Request.Path, duracao);
    }
}
