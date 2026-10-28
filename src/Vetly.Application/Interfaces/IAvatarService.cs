using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Comum;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Avatar do pet calculado no servidor (RN-112 a RN-114), com a mesma regra de
/// acesso do board.
/// </summary>
public interface IAvatarService
{
    /// <summary>
    /// Condição atual do avatar: saúde, humor, estado, pelo, motivos e falas. Grava a
    /// condição do dia no histórico (upsert), sem falhar a leitura se o histórico
    /// estiver indisponível.
    /// </summary>
    Task<AvatarDoPetDto> ObterAsync(Guid animalId);

    /// <summary>
    /// Histórico diário do avatar, paginado, com ordenação na whitelist
    /// <see cref="CamposDeOrdenacao.HistoricoDoAvatar"/> (padrão: data decrescente).
    /// </summary>
    Task<ResultadoPaginado<RegistroDiarioDoAvatarDto>> ObterHistoricoAsync(
        Guid animalId, Paginacao paginacao, Ordenacao? ordenacao = null);
}
