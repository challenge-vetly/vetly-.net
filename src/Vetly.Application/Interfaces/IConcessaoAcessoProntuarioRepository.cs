using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório para <see cref="ConcessaoAcessoProntuario"/>.</summary>
public interface IConcessaoAcessoProntuarioRepository : IRepositoryBase<ConcessaoAcessoProntuario>
{
    /// <summary>Retorna a concessão ativa (não revogada, não expirada) mais recente para o par vet+animal, se houver.</summary>
    Task<ConcessaoAcessoProntuario?> ObterAtivaAsync(Guid veterinarioId, Guid animalId, DateTime agora);

    /// <summary>Retorna todas as concessões ativas de um veterinário.</summary>
    Task<IEnumerable<ConcessaoAcessoProntuario>> ObterAtivasPorVeterinarioAsync(Guid veterinarioId, DateTime agora);
}
