using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositorio para <see cref="LembreteAgendado"/>.</summary>
public interface ILembreteRepository : IRepositoryBase<LembreteAgendado>
{
    /// <summary>Retorna lembretes pendentes de resposta para um tutor.</summary>
    Task<IEnumerable<LembreteAgendado>> ObterPendentesPorTutorAsync(Guid tutorId);

    /// <summary>
    /// Réguas ainda abertas com evento até a data informada (RN-094/RN-095).
    ///
    /// Aberta é a que o Responsável não respondeu e que ainda não escalou para a
    /// clínica: depois do alerta a régua já cumpriu o papel dela, e seguir tentando
    /// seria perseguir, não lembrar.
    /// </summary>
    Task<IEnumerable<LembreteAgendado>> ObterAtivosAteAsync(DateTime limite);
}
