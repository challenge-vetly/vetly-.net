using System.Security.Claims;
using Vetly.Application.Interfaces;

namespace Vetly.API.Services;

/// <summary>
/// Implementação de ICurrentUserService que lê a identidade do usuário a partir das
/// claims do HttpContext atual (JWT já validado pelo middleware de autenticação).
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? EntidadeId
    {
        get
        {
            var valor = _httpContextAccessor.HttpContext?.User.FindFirstValue("entidadeId");
            return Guid.TryParse(valor, out var id) ? id : null;
        }
    }

    public string? Role =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
}
