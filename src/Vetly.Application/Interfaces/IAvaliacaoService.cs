using Vetly.Application.DTOs.Avaliacao;
using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Avaliação do atendimento e a reputação que sai dela (RN-055/RN-057).
/// </summary>
public interface IAvaliacaoService
{
    /// <summary>
    /// Avalia um atendimento realizado (RN-055). Só o Responsável atendido avalia, e
    /// só uma vez por consulta.
    /// </summary>
    Task<AvaliacaoDto> AvaliarAsync(Guid consultaId, CriarAvaliacaoDto dto);

    /// <summary>Reputação de um veterinário, com distribuição das notas (RN-057).</summary>
    Task<ReputacaoDto> ObterReputacaoAsync(Guid veterinarioId);

    /// <summary>Resposta pública do veterinário à avaliação. Uma só (RN-055).</summary>
    Task<AvaliacaoDto> ResponderAsync(Guid avaliacaoId, ResponderAvaliacaoDto dto);

    /// <summary>
    /// Esconde o comentário por moderação. A nota continua contando na média — o
    /// contrário transformaria a moderação em ferramenta para apagar crítica.
    /// </summary>
    Task<AvaliacaoDto> ModerarAsync(Guid avaliacaoId, ModerarAvaliacaoDto dto);
}

/// <summary>Repositório das avaliações (RN-055/RN-057).</summary>
public interface IAvaliacaoRepository
{
    Task<Avaliacao?> ObterPorIdAsync(Guid id);

    /// <summary>Avaliação de uma consulta, se houver. É como se impede a segunda.</summary>
    Task<Avaliacao?> ObterDaConsultaAsync(Guid consultaId);

    /// <summary>Avaliações de um veterinário. É a base do recálculo da reputação.</summary>
    Task<IEnumerable<Avaliacao>> ObterDoVeterinarioAsync(Guid veterinarioId);

    Task AdicionarAsync(Avaliacao avaliacao);
    void Atualizar(Avaliacao avaliacao);

    Task<int> SalvarAsync();
}
