using Vetly.Application.DTOs.Dispositivo;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato do serviço de dispositivos do Responsável (RN-007/RN-092).
/// </summary>
public interface IDispositivoService
{
    /// <summary>Dispositivos ativos do Responsável.</summary>
    Task<IEnumerable<DispositivoDto>> ObterDoTutorAsync(Guid tutorId);

    /// <summary>
    /// Registra um dispositivo para push. Idempotente por push token: reinstalar o
    /// app reaproveita o registro em vez de duplicar.
    /// </summary>
    Task<DispositivoDto> RegistrarAsync(Guid tutorId, RegistrarDispositivoDto dto);

    /// <summary>Remove o dispositivo (remoção lógica).</summary>
    Task RemoverAsync(Guid tutorId, Guid dispositivoId);
}
