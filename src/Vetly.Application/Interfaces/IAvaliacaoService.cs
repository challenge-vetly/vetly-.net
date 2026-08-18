using Vetly.Application.DTOs.Avaliacao;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de avaliações (RN-076..082).</summary>
public interface IAvaliacaoService
{
    /// <summary>Retorna uma avaliação pelo ID.</summary>
    Task<AvaliacaoDto> ObterPorIdAsync(Guid id);

    /// <summary>Publica a avaliação de uma consulta realizada (RN-076/077).</summary>
    Task<AvaliacaoDto> CriarAsync(Guid consultaId, CriarAvaliacaoDto dto);

    /// <summary>Edita uma avaliação existente, só dentro da janela de 48h (RN-082).</summary>
    Task<AvaliacaoDto> EditarAsync(Guid avaliacaoId, EditarAvaliacaoDto dto);

    /// <summary>Registra a resposta pública do veterinário (RN-079).</summary>
    Task<AvaliacaoDto> ResponderAsync(Guid avaliacaoId, ResponderAvaliacaoDto dto);

    /// <summary>Aplica moderação ao comentário de uma avaliação (RN-080).</summary>
    Task<AvaliacaoDto> ModerarAsync(Guid avaliacaoId, ModerarAvaliacaoDto dto);

    /// <summary>Lista as avaliações não invalidadas recebidas por um veterinário.</summary>
    Task<IEnumerable<AvaliacaoDto>> ObterPorVeterinarioAsync(Guid veterinarioId);

    /// <summary>
    /// Invalida a avaliação de uma consulta cancelada/reembolsada, se existir, e recalcula
    /// a reputação do veterinário (RN-081). No-op se a consulta não tiver avaliação.
    /// </summary>
    Task InvalidarPorCancelamentoAsync(Guid consultaId, DateTime agora);
}
