using System.Net;
using Microsoft.AspNetCore.Mvc;
using Vetly.Application.Exceptions;
using Vetly.Application.Observability;

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
        // O CorrelationIdMiddleware ja resolveu o identificador e o gravou aqui; ler
        // dali (em vez de gerar outro) e o que faz o corpo do erro apontar para o mesmo
        // trace que o log e o backend de tracing conhecem.
        //
        // Ele nao aparece mais nos templates de log abaixo de proposito: o enriquecedor
        // do Serilog carimba CorrelationId e TraceId em TODA linha, e repetir a
        // propriedade no template so duplicaria o campo no JSON.
        var correlationId = context.TraceIdentifier;

        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("NotFound: {Message}", ex.Message);
            Instrumentar("NAO-ENCONTRADO", ex);
            await EscreverRespostaAsync(context, HttpStatusCode.NotFound, ex.Message, correlationId);
        }
        catch (Application.Exceptions.ValidationException ex)
        {
            _logger.LogWarning("ValidationError: {Errors}", ex.Erros);
            Instrumentar("VALIDACAO", ex);
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
        catch (AcessoNegadoException ex)
        {
            _logger.LogWarning("AcessoNegado [{Codigo}]: {Message}", ex.Codigo, ex.Message);
            Instrumentar(ex.Codigo, ex);
            await EscreverRespostaAsync(context, HttpStatusCode.Forbidden,
                ex.Message, correlationId, ex.Codigo);
        }
        catch (ConflitoDeEstadoException ex)
        {
            _logger.LogWarning("Conflito [{Codigo}]: {Message}", ex.Codigo, ex.Message);
            Instrumentar(ex.Codigo, ex);
            await EscreverRespostaAsync(context, HttpStatusCode.Conflict,
                ex.Message, correlationId, ex.Codigo);
        }
        catch (BusinessRuleException ex)
        {
            _logger.LogWarning("BusinessRule [{Codigo}]: {Message}", ex.Codigo, ex.Message);
            Instrumentar(ex.Codigo, ex);
            await EscreverRespostaAsync(context, HttpStatusCode.UnprocessableEntity,
                ex.Message, correlationId, ex.Codigo);
        }
        catch (DependenciaIndisponivelException ex)
        {
            // 503 e nao 422: nao ha regra violada nem nada que o cliente possa
            // corrigir no payload. A resposta certa e "tente de novo" (§2.4).
            _logger.LogWarning(
                "DependenciaIndisponivel [{Dependencia}]: {Message}", ex.Dependencia, ex.Message);

            // A RN afetada e opcional nesta excecao; sem ela, a dependencia que caiu e
            // a informacao util — "DEPENDENCIA-Ollama" agrupa por quem esta fora.
            Instrumentar(ex.Codigo ?? $"DEPENDENCIA-{ex.Dependencia}", ex);
            await EscreverRespostaAsync(context, HttpStatusCode.ServiceUnavailable,
                ex.Message, correlationId, ex.Codigo);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("InvalidOperation: {Message}", ex.Message);
            Instrumentar("OPERACAO-INVALIDA", ex);
            await EscreverRespostaAsync(context, HttpStatusCode.UnprocessableEntity, ex.Message, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UnhandledException: {Message}", ex.Message);
            Instrumentar("ERRO-INTERNO", ex);
            await EscreverRespostaAsync(context, HttpStatusCode.InternalServerError,
                "Ocorreu um erro interno. Tente novamente mais tarde.", correlationId);
        }
    }

    /// <summary>
    /// Contabiliza a regra violada e marca o span da requisicao como falho.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Este e o unico ponto do sistema por onde <b>toda</b> excecao de negocio passa —
    /// o que faz dele o lugar certo para instrumentar. Contar a violacao dentro de cada
    /// servico significaria repetir a chamada em dezenas de arquivos e esquecer em
    /// alguns; aqui, uma RN nova ja nasce medida.
    /// </para>
    /// <para>
    /// Marcar o span importa porque, do ponto de vista do transporte, um 422 e uma
    /// resposta perfeitamente bem-sucedida: o trace sairia verde. Quem investiga
    /// "por que o agendamento nao completou" precisa que o span diga que a operacao
    /// terminou em RN-035, e nao que tudo correu bem.
    /// </para>
    /// </remarks>
    /// <param name="codigo">Codigo da regra (RN-035, RN-060, ...) ou da falha tecnica.</param>
    /// <param name="excecao">Excecao capturada.</param>
    private static void Instrumentar(string codigo, Exception excecao)
    {
        // Tag de baixa cardinalidade por construcao: o conjunto de codigos e finito e
        // conhecido, ao contrario da mensagem, que carrega ids e nomes.
        VetlyTelemetry.RegrasVioladas.Add(1, new KeyValuePair<string, object?>("codigo", codigo));

        VetlyTelemetry.RegistrarFalhaNoSpanAtual(excecao);
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
                HttpStatusCode.Forbidden => "Acesso negado",
                HttpStatusCode.Conflict => "Conflito de estado",
                HttpStatusCode.UnprocessableEntity => "Regra de negocio violada",
                HttpStatusCode.ServiceUnavailable => "Dependencia indisponivel",
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
