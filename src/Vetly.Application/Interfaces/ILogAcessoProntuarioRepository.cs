using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório para <see cref="LogAcessoProntuario"/>.</summary>
public interface ILogAcessoProntuarioRepository : IRepositoryBase<LogAcessoProntuario>
{
    /// <summary>Retorna todo o log de acessos de um animal, mais recente primeiro — visível ao Responsável (RN-086).</summary>
    Task<IEnumerable<LogAcessoProntuario>> ObterPorAnimalAsync(Guid animalId);
}
