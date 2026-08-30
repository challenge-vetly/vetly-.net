using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Vetly.Application.Interfaces;
using Vetly.Domain.Enums;

namespace Vetly.API.Filters;

/// <summary>
/// Marca uma rota como acessível ao Responsável <b>antes</b> de ele consentir.
///
/// É o mínimo necessário para que o consentimento possa acontecer: autenticação,
/// leitura do próprio perfil e as rotas de consentimento em si. Tudo o mais fica
/// bloqueado até a base legal existir (RN-060).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class IsentoDeConsentimentoAttribute : Attribute;

/// <summary>
/// Bloqueia as rotas de negócio do Responsável enquanto ele não registra o
/// consentimento de atendimento (RN-060): a base legal precede o tratamento de dados.
///
/// Falha fechado por decisão: o filtro roda em tudo, e a rota que precisa funcionar
/// antes do consentimento se declara com <see cref="IsentoDeConsentimentoAttribute"/>.
/// Esquecer de isentar uma rota deixa o usuário travado — esquecer de proteger uma
/// rota deixaria a plataforma tratando dados sem base legal.
/// </summary>
public class ConsentimentoAtendimentoFilter : IAsyncActionFilter
{
    private readonly IUsuarioAtual _usuario;
    private readonly ITutorRepository _tutorRepo;

    public ConsentimentoAtendimentoFilter(IUsuarioAtual usuario, ITutorRepository tutorRepo)
    {
        _usuario = usuario;
        _tutorRepo = tutorRepo;
    }

    /// <inheritdoc/>
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Só vale para o Responsável: vet e admin não passam por este portão
        if (!_usuario.EhTutor || _usuario.TutorId is not { } tutorId)
        {
            await next();
            return;
        }

        if (RotaIsenta(context))
        {
            await next();
            return;
        }

        var tutor = await _tutorRepo.ObterPorIdAsync(tutorId);

        if (tutor is null || !tutor.Consentiu(FinalidadeConsentimento.Atendimento))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Regra de negocio violada",
                Detail = "E preciso registrar o consentimento de atendimento antes de usar a plataforma.",
                Extensions =
                {
                    ["codigo"] = "RN-060",
                    ["correlationId"] = context.HttpContext.TraceIdentifier
                }
            })
            {
                StatusCode = StatusCodes.Status422UnprocessableEntity,
                ContentTypes = { "application/problem+json" }
            };

            return;
        }

        await next();
    }

    private static bool RotaIsenta(ActionExecutingContext context) =>
        context.ActionDescriptor.EndpointMetadata.OfType<IsentoDeConsentimentoAttribute>().Any();
}
