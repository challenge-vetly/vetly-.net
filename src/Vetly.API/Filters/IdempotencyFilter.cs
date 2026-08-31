using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Infrastructure.Data;

namespace Vetly.API.Filters;

/// <summary>
/// Marca uma rota como idempotente: ela passa a exigir o header
/// <c>Idempotency-Key</c> e a reaproveitar a resposta da primeira execução (§2.5).
///
/// Vale para o que não pode acontecer duas vezes — reservar horário, criar cobrança,
/// cancelar com estorno. O app repete envio por natureza: rede oscila, o usuário toca
/// de novo, o cliente faz retry automático.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotenteAttribute : Attribute;

/// <summary>
/// Guarda e reaproveita a resposta das rotas marcadas com
/// <see cref="IdempotenteAttribute"/> (§2.5).
///
/// A chave é o trio <c>(chave, usuário, rota)</c>. Reenvio dentro de 24 horas devolve
/// a resposta original sem executar a ação de novo.
/// </summary>
public class IdempotencyFilter : IAsyncActionFilter
{
    /// <summary>Header que carrega a chave de idempotência.</summary>
    public const string Header = "Idempotency-Key";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly VetlyDbContext _context;
    private readonly IUsuarioAtual _usuario;
    private readonly ILogger<IdempotencyFilter> _logger;

    public IdempotencyFilter(VetlyDbContext context, IUsuarioAtual usuario, ILogger<IdempotencyFilter> logger)
    {
        _context = context;
        _usuario = usuario;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!RotaIdempotente(context))
        {
            await next();
            return;
        }

        var chave = context.HttpContext.Request.Headers[Header].ToString();

        if (string.IsNullOrWhiteSpace(chave))
        {
            context.Result = Problema(context,
                StatusCodes.Status400BadRequest,
                $"O header {Header} e obrigatorio nesta rota.",
                "IDEMPOTENCIA-001");
            return;
        }

        if (chave.Length > 100)
        {
            context.Result = Problema(context,
                StatusCodes.Status400BadRequest,
                $"O header {Header} deve ter no maximo 100 caracteres.",
                "IDEMPOTENCIA-001");
            return;
        }

        var usuarioId = _usuario.UsuarioId ?? Guid.Empty;
        var rota = $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";

        var existente = await _context.RegistrosDeIdempotencia
            .FirstOrDefaultAsync(r => r.Chave == chave && r.UsuarioId == usuarioId && r.Rota == rota);

        if (existente is not null && existente.Vigente(DateTime.UtcNow))
        {
            _logger.LogInformation(
                "Requisicao idempotente reaproveitada | rota={Rota} chave={Chave}", rota, chave);

            // Devolve exatamente o que a primeira execucao devolveu, sem executar de novo
            context.Result = new ContentResult
            {
                StatusCode = existente.StatusHttp,
                Content = existente.Resposta,
                ContentType = "application/json"
            };
            return;
        }

        var executado = await next();

        // So se guarda o que deu certo: erro deve poder ser corrigido e reenviado com a
        // mesma chave, senao o cliente ficaria preso ao primeiro erro por 24 horas.
        if (executado.Exception is not null)
            return;

        var (status, corpo) = ExtrairResposta(executado.Result);

        if (status is < 200 or >= 300)
            return;

        try
        {
            _context.RegistrosDeIdempotencia.Add(
                new RegistroIdempotencia(chave, usuarioId, rota, status, corpo));

            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Duas requisicoes simultaneas com a mesma chave: o indice unico barra a
            // segunda gravacao. A acao ja executou, entao a resposta segue normalmente —
            // e o proximo reenvio ja encontra o registro.
            _logger.LogWarning(
                "Chave de idempotencia gravada em paralelo | rota={Rota} chave={Chave}", rota, chave);
        }
    }

    private static bool RotaIdempotente(ActionExecutingContext context) =>
        context.ActionDescriptor.EndpointMetadata.OfType<IdempotenteAttribute>().Any();

    private static (int Status, string? Corpo) ExtrairResposta(IActionResult? resultado) => resultado switch
    {
        ObjectResult objeto => (objeto.StatusCode ?? StatusCodes.Status200OK,
                                objeto.Value is null ? null : JsonSerializer.Serialize(objeto.Value, Json)),
        StatusCodeResult semCorpo => (semCorpo.StatusCode, null),
        _ => (StatusCodes.Status200OK, null)
    };

    private static ObjectResult Problema(ActionExecutingContext context, int status, string detalhe, string codigo) =>
        new(new ProblemDetails
        {
            Status = status,
            Title = "Erro de validacao",
            Detail = detalhe,
            Extensions =
            {
                ["codigo"] = codigo,
                ["correlationId"] = context.HttpContext.TraceIdentifier
            }
        })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
}
