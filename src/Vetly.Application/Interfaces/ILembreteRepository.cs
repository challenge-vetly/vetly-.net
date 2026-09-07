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

    /// <summary>
    /// Réguas que esgotaram as três tentativas sem resposta (RN-095, §6.4).
    ///
    /// É o outro lado da <see cref="ObterAtivosAteAsync"/>: aquela devolve o que ainda
    /// vai ser tentado, esta devolve o que já não vai — e é justamente esta que a
    /// clínica precisa ver. O alerta era gravado e não chegava a lugar nenhum antes de
    /// existir alguém para lê-lo.
    ///
    /// Só o que segue sem resposta: se o Responsável respondeu depois do alerta, a
    /// régua cumpriu o papel e o animal deixou de ser um caso em aberto.
    /// </summary>
    Task<IEnumerable<LembreteAgendado>> ObterEscaladosParaClinicaAsync(DateTime desde);
}
