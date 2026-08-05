using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositorio para <see cref="LembreteAgendado"/>.</summary>
public interface ILembreteRepository : IRepositoryBase<LembreteAgendado>
{
    /// <summary>Retorna lembretes pendentes de resposta para um responsavel.</summary>
    Task<IEnumerable<LembreteAgendado>> ObterPendentesPorResponsavelAsync(Guid responsavelId);
}
