namespace Vetly.Application.Interfaces;

/// <summary>
/// Expõe a identidade do usuário autenticado na requisição atual, lida a partir das
/// claims do JWT, para que os Services de Application possam aplicar checagens de
/// posse (ex: veterinário só acessa a própria agenda) sem depender de HttpContext.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Id da entidade de negócio associada ao token (VeterinarioId para role
    /// Veterinario, EmpresaId para role Admin, ResponsavelId para role Responsavel).
    /// Nulo quando o token foi emitido sem a claim "entidadeId".
    /// </summary>
    Guid? EntidadeId { get; }

    /// <summary>Role do usuário autenticado (Admin, Veterinario ou Responsavel).</summary>
    string? Role { get; }
}
