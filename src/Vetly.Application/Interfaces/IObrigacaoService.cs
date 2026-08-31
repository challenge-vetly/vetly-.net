using Vetly.Application.DTOs.Obrigacao;
using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Obrigações de cuidado do animal e o board que as mostra (RN-045/RN-046).
/// </summary>
public interface IObrigacaoService
{
    /// <summary>Board de obrigações de um animal, das mais urgentes às menos.</summary>
    Task<BoardDeObrigacoesDto> ObterBoardAsync(Guid animalId, bool incluirArquivadas = false);

    /// <summary>Cria uma obrigação recorrente de cuidado (RN-045).</summary>
    Task<ObrigacaoPetDto> CriarAsync(Guid animalId, CriarObrigacaoDto dto);

    /// <summary>
    /// Registra o cumprimento e empurra o próximo vencimento. Obrigação de uma vez só
    /// é arquivada ao ser cumprida, em vez de ficar eternamente vencida no board.
    /// </summary>
    Task<ObrigacaoPetDto> CumprirAsync(Guid obrigacaoId, CumprirObrigacaoDto dto);

    /// <summary>Tira a obrigação do board sem apagar do histórico.</summary>
    Task<ObrigacaoPetDto> ArquivarAsync(Guid obrigacaoId);

    /// <summary>
    /// Cria obrigações a partir da carteira de vacinação já cadastrada (RN-046).
    /// É idempotente: chamar de novo não duplica o que já existe.
    /// </summary>
    Task<IEnumerable<ObrigacaoPetDto>> DerivarDaCarteiraAsync(Guid animalId);
}

/// <summary>Repositório das obrigações de cuidado (RN-045).</summary>
public interface IObrigacaoRepository
{
    Task<ObrigacaoPet?> ObterPorIdAsync(Guid id);

    /// <summary>Obrigações de um animal. Arquivadas ficam de fora por padrão.</summary>
    Task<IEnumerable<ObrigacaoPet>> ObterDoAnimalAsync(Guid animalId, bool incluirArquivadas = false);

    /// <summary>
    /// Obrigações vencidas ou vencendo até uma data, em toda a base. É o que a rotina
    /// de lembretes varre para disparar aviso (RN-094).
    /// </summary>
    Task<IEnumerable<ObrigacaoPet>> ObterVencendoAteAsync(DateTime limite);

    Task AdicionarAsync(ObrigacaoPet obrigacao);
    void Atualizar(ObrigacaoPet obrigacao);

    Task<int> SalvarAsync();
}
