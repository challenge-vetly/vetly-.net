using Vetly.Application.DTOs.Animal;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Avatar do pet calculado no servidor (RN-112 a RN-114), com a mesma regra de
/// acesso do board.
/// </summary>
public interface IAvatarService
{
    /// <summary>
    /// Condição atual do avatar: saúde, humor, estado, pelo, motivos e falas.
    /// </summary>
    Task<AvatarDoPetDto> ObterAsync(Guid animalId);
}
