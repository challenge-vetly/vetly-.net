using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório para <see cref="RefreshToken"/>.</summary>
public interface IRefreshTokenRepository : IRepositoryBase<RefreshToken>
{
    /// <summary>Busca um token pelo hash — é assim que o refresh chega.</summary>
    Task<RefreshToken?> ObterPorHashAsync(string hash);

    /// <summary>
    /// Revoga todos os tokens ativos de um usuário. Usado no logout de todas as
    /// sessões e no offboarding, que encerra o acesso imediatamente (RN-022).
    /// </summary>
    Task<int> RevogarTodosDoUsuarioAsync(Guid usuarioId, DateTime quando);
}
