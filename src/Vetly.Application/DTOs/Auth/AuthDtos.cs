using System.ComponentModel.DataAnnotations;
using Vetly.Domain.Enums;

namespace Vetly.Application.DTOs.Auth;

/// <summary>Dados de cadastro do Responsável pelo app (RN-060).</summary>
public class RegistrarTutorDto
{
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "O telefone é obrigatório.")]
    [MaxLength(20)]
    public string Telefone { get; set; } = string.Empty;

    /// <summary>Senha de acesso ao app. Mínimo de 8 caracteres.</summary>
    [Required(ErrorMessage = "A senha é obrigatória.")]
    [MinLength(8, ErrorMessage = "A senha deve ter ao menos 8 caracteres.")]
    [MaxLength(128)]
    public string Senha { get; set; } = string.Empty;
}

/// <summary>Credenciais de acesso.</summary>
public class LoginDto
{
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "A senha é obrigatória.")]
    public string Senha { get; set; } = string.Empty;
}

/// <summary>Refresh token entregue no login, usado para renovar o acesso.</summary>
public class RefreshDto
{
    [Required(ErrorMessage = "O refresh token é obrigatório.")]
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>Par de tokens emitido no login e em cada renovação.</summary>
public class TokenEmitidoDto
{
    /// <summary>Token de acesso JWT.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Refresh token rotativo — muda a cada uso.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime ExpiraEm { get; set; }

    /// <summary>Role do usuário autenticado.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Id do Responsável, quando a role é Tutor.</summary>
    public Guid? TutorId { get; set; }

    /// <summary>Id do veterinário, quando a role é Veterinario.</summary>
    public Guid? VeterinarioId { get; set; }

    /// <summary>
    /// Verdadeiro quando falta o consentimento de atendimento — o app deve levar o
    /// Responsável à tela de consentimento antes de qualquer outra ação (RN-060).
    /// </summary>
    public bool ConsentimentoPendente { get; set; }

    /// <summary>
    /// Verdadeiro quando o veterinário ainda usa a senha temporária gerada no
    /// cadastro pelo Admin e precisa trocá-la (P-05).
    /// </summary>
    public bool SenhaTemporaria { get; set; }
}

/// <summary>Troca de senha do usuário autenticado.</summary>
public class TrocarSenhaDto
{
    [Required(ErrorMessage = "A senha atual é obrigatória.")]
    public string SenhaAtual { get; set; } = string.Empty;

    [Required(ErrorMessage = "A nova senha é obrigatória.")]
    [MinLength(8, ErrorMessage = "A nova senha deve ter ao menos 8 caracteres.")]
    [MaxLength(128)]
    public string NovaSenha { get; set; } = string.Empty;
}

/// <summary>Perfil do usuário autenticado e o que ainda falta ele resolver.</summary>
public class PerfilDto
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public TipoUsuario TipoUsuario { get; set; }

    /// <summary>
    /// Pendências que bloqueiam ou limitam o uso: consentimento (RN-060),
    /// CRMV não validado (RN-107), cadastro incompleto.
    /// </summary>
    public List<string> Pendencias { get; set; } = [];
}
