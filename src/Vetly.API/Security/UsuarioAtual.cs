using System.Security.Claims;
using Vetly.Application.Interfaces;

namespace Vetly.API.Security;

/// <summary>
/// Lê a identidade e o escopo do usuário a partir das claims do token da requisição
/// em curso (RN-105/RN-106).
///
/// Fica na camada de API porque é aqui que existe <see cref="HttpContext"/>; os
/// serviços dependem apenas da abstração <see cref="IUsuarioAtual"/>.
/// </summary>
public class UsuarioAtual : IUsuarioAtual
{
    private readonly ClaimsPrincipal? _usuario;

    public UsuarioAtual(IHttpContextAccessor accessor) => _usuario = accessor.HttpContext?.User;

    /// <inheritdoc/>
    public Guid? UsuarioId => LerGuid(ClaimTypes.NameIdentifier);

    /// <inheritdoc/>
    public string? Role => _usuario?.FindFirstValue(ClaimTypes.Role);

    /// <inheritdoc/>
    public Guid? TutorId => LerGuid("tutorId");

    /// <inheritdoc/>
    public Guid? VeterinarioId => LerGuid("veterinarioId");

    /// <inheritdoc/>
    public bool Autenticado => _usuario?.Identity?.IsAuthenticated == true;

    /// <inheritdoc/>
    public bool EhAdmin => _usuario?.IsInRole("Admin") == true;

    /// <inheritdoc/>
    public bool EhTutor => _usuario?.IsInRole("Tutor") == true;

    /// <inheritdoc/>
    public bool EhVeterinario => _usuario?.IsInRole("Veterinario") == true;

    private Guid? LerGuid(string claim) =>
        Guid.TryParse(_usuario?.FindFirstValue(claim), out var valor) ? valor : null;
}
