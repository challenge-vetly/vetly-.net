using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Vetly.Application.Services;

namespace Vetly.API.Filters;

/// <summary>
/// Marca uma rota como acessível ao veterinário desativado.
///
/// A RN-024 é restritiva por natureza: o profissional desligado perde acesso aos
/// prontuários e pode <b>apenas</b> solicitar o extrato dos atendimentos que realizou,
/// sem dados pessoais do Responsável ou do animal. Só as rotas com este atributo
/// escapam do bloqueio.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class PermitidoAoVetDesativadoAttribute : Attribute;

/// <summary>
/// Encerra o acesso do veterinário desativado (RN-022) preservando apenas o que a
/// RN-024 garante a ele.
///
/// O bloqueio é por role: quem foi desativado recebe <c>VetDesativado</c> no login e
/// na renovação, e daí em diante bate neste filtro em qualquer rota que não esteja
/// explicitamente liberada. Falha fechado, como o portão de consentimento.
/// </summary>
public class VetDesativadoFilter : IAsyncActionFilter
{
    /// <inheritdoc/>
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.User.IsInRole(AuthService.RoleDoVetDesativado))
            return next();

        if (RotaLiberada(context))
            return next();

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Acesso negado",
            Detail = "Cadastro desativado. O acesso permitido e apenas ao extrato dos atendimentos realizados.",
            Extensions =
            {
                ["codigo"] = "RN-024",
                ["correlationId"] = context.HttpContext.TraceIdentifier
            }
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
            ContentTypes = { "application/problem+json" }
        };

        return Task.CompletedTask;
    }

    private static bool RotaLiberada(ActionExecutingContext context) =>
        context.ActionDescriptor.EndpointMetadata.OfType<PermitidoAoVetDesativadoAttribute>().Any();
}
