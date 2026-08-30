namespace Vetly.Application.Interfaces;

/// <summary>
/// Identidade e escopo do usuário da requisição em curso, lidos das claims do token.
///
/// É o que permite ao serviço aplicar escopo por linha (RN-105/RN-106) sem que o
/// controller precise repassar ids — e sem que um cliente consiga escolher de quem
/// são os dados que vai receber, passando o id de outro na query string.
/// </summary>
public interface IUsuarioAtual
{
    /// <summary>Id do usuário autenticado. Nulo em requisição anônima.</summary>
    Guid? UsuarioId { get; }

    /// <summary>Role do token: <c>Tutor</c>, <c>Veterinario</c> ou <c>Admin</c>.</summary>
    string? Role { get; }

    /// <summary>Id do Responsável, quando o token é de um Tutor.</summary>
    Guid? TutorId { get; }

    /// <summary>Id do veterinário, quando o token é de um Veterinario.</summary>
    Guid? VeterinarioId { get; }

    /// <summary>Há um usuário autenticado nesta requisição.</summary>
    bool Autenticado { get; }

    /// <summary>Administrador — enxerga o consolidado da unidade (RN-106).</summary>
    bool EhAdmin { get; }

    /// <summary>Responsável — enxerga apenas os próprios dados.</summary>
    bool EhTutor { get; }

    /// <summary>Veterinário — enxerga o próprio escopo de atendimento (RN-105).</summary>
    bool EhVeterinario { get; }
}
