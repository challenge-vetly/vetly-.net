using System.Security.Claims;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Token de acesso emitido, com o instante em que expira.</summary>
/// <param name="Token">JWT assinado.</param>
/// <param name="ExpiraEm">Expiração do token de acesso (UTC).</param>
public readonly record struct TokenDeAcesso(string Token, DateTime ExpiraEm);

/// <summary>
/// Porta para a emissão do token de acesso (§2.2 do documento de engenharia).
/// </summary>
public interface IGeradorDeTokenJwt
{
    /// <summary>
    /// Emite um token de acesso com as claims de identidade e escopo.
    /// As claims de escopo (<c>tutorId</c>, <c>veterinarioId</c>, <c>empresaId</c>)
    /// são o que permite ao serviço validar posse por linha (RN-105/RN-106).
    /// </summary>
    TokenDeAcesso Emitir(Guid usuarioId, string nome, string role, TipoUsuario tipoUsuario, IEnumerable<Claim>? claimsAdicionais = null);

    /// <summary>Gera um refresh token opaco e o hash correspondente para persistência.</summary>
    (string Token, string Hash) GerarRefreshToken();

    /// <summary>Calcula o hash de um refresh token recebido, para busca na base.</summary>
    string CalcularHash(string refreshToken);
}
