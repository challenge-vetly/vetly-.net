using Vetly.Application.DTOs.Auth;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato do serviço de autenticação e sessão (§3.1 do documento de engenharia).
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Cadastra o Responsável pelo app e já devolve a sessão. O consentimento LGPD
    /// é registrado em seguida, antes de qualquer ação de negócio (RN-060).
    /// </summary>
    Task<TokenEmitidoDto> RegistrarTutorAsync(RegistrarTutorDto dto);

    /// <summary>Autentica por e-mail e senha e emite o par de tokens.</summary>
    Task<TokenEmitidoDto> LoginAsync(LoginDto dto);

    /// <summary>
    /// Renova o acesso rotacionando o refresh token: o token usado é revogado e
    /// aponta para o novo, o que torna reuso detectável.
    /// </summary>
    Task<TokenEmitidoDto> RenovarAsync(RefreshDto dto);

    /// <summary>Encerra a sessão revogando o refresh token informado.</summary>
    Task EncerrarSessaoAsync(RefreshDto dto);

    /// <summary>Perfil do usuário autenticado e suas pendências (RN-060, RN-107).</summary>
    Task<PerfilDto> ObterPerfilAsync(Guid usuarioId);

    /// <summary>
    /// Troca a senha do usuário autenticado. É o que fecha o ciclo da senha temporária
    /// do veterinário (P-05). Derruba as demais sessões: se a senha antiga vazou, o
    /// refresh dela não pode continuar valendo.
    /// </summary>
    Task TrocarSenhaAsync(Guid usuarioId, TrocarSenhaDto dto);
}
