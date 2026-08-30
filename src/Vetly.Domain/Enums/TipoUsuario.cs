namespace Vetly.Domain.Enums;

/// <summary>
/// Tipo de usuário por trás de uma credencial. Define a role no token e o escopo
/// de acesso por linha (RN-105/RN-106).
/// </summary>
public enum TipoUsuario
{
    /// <summary>Responsável pelo pet — opera inteiramente pelo app.</summary>
    Tutor = 1,

    /// <summary>Veterinário, autônomo ou vinculado a uma empresa.</summary>
    Veterinario = 2,

    /// <summary>Administrador da unidade.</summary>
    Admin = 3
}
