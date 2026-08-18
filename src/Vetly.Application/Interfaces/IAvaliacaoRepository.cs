using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório para <see cref="Avaliacao"/>.</summary>
public interface IAvaliacaoRepository : IRepositoryBase<Avaliacao>
{
    /// <summary>Retorna a avaliação de uma consulta, se existir (RN-076: única por consulta).</summary>
    Task<Avaliacao?> ObterPorConsultaAsync(Guid consultaId);

    /// <summary>Retorna todas as avaliações não invalidadas de um veterinário.</summary>
    Task<IEnumerable<Avaliacao>> ObterValidasPorVeterinarioAsync(Guid veterinarioId);
}
