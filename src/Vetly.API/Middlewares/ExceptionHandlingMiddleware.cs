using System.Net;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.Exceptions;

namespace Vetly.API.Middlewares;

/// <summary>
/// Middleware global de tratamento de excecoes.
/// Converte excecoes de dominio em respostas ProblemDetails (RFC 7807).
/// Garante que nenhuma excecao nao tratada vaze para o cliente com stacktrace.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Cada request recebe um correlationId para rastreabilidade nos logs
        var correlationId = context.TraceIdentifier;

        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("CorrelationId={CorrelationId} | NotFound: {Message}", correlationId, ex.Message);
            await EscreverRespostaAsync(context, HttpStatusCode.NotFound, ex.Message, correlationId);
        }
        catch (Application.Exceptions.ValidationException ex)
        {
            _logger.LogWarning("CorrelationId={CorrelationId} | ValidationError: {Errors}", correlationId, ex.Erros);
            var details = new ValidationProblemDetails(ex.Erros)
            {
                Status = (int)HttpStatusCode.BadRequest,
                Title = "Erro de validacao",
                Detail = ex.Message,
                Extensions = { ["correlationId"] = correlationId }
            };
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(details);
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning("CorrelationId={CorrelationId} | BusinessRule [{Codigo}]: {Message}", correlationId, ex.Codigo, ex.Message);
            await EscreverRespostaAsync(context, HttpStatusCode.UnprocessableEntity,
                ex.Message, correlationId, ex.Codigo);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("CorrelationId={CorrelationId} | InvalidOperation: {Message}", correlationId, ex.Message);
            await EscreverRespostaAsync(context, HttpStatusCode.UnprocessableEntity, ex.Message, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CorrelationId={CorrelationId} | UnhandledException: {Message}", correlationId, ex.Message);
            await EscreverRespostaAsync(context, HttpStatusCode.InternalServerError,
                "Ocorreu um erro interno. Tente novamente mais tarde.", correlationId);
        }
    }

    private static async Task EscreverRespostaAsync(
        HttpContext context, HttpStatusCode status, string detalhe,
        string correlationId, string? codigo = null)
    {
        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = status switch
            {
                HttpStatusCode.NotFound => "Recurso nao encontrado",
                HttpStatusCode.UnprocessableEntity => "Regra de negocio violada",
                _ => "Erro interno do servidor"
            },
            Detail = detalhe,
            Extensions =
            {
                ["correlationId"] = correlationId
            }
        };

        if (codigo is not null)
            problem.Extensions["codigo"] = codigo;

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
