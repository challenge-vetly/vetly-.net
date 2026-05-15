using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositorio para <see cref="LembreteAgendado"/>.</summary>
public interface ILembreteRepository : IRepositoryBase<LembreteAgendado>
{
    /// <summary>Retorna lembretes pendentes de resposta para um tutor.</summary>
    Task<IEnumerable<LembreteAgendado>> ObterPendentesPorTutorAsync(Guid tutorId);
}
