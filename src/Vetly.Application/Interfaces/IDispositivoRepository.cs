using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório para <see cref="Dispositivo"/> (RN-007/RN-092).</summary>
public interface IDispositivoRepository : IRepositoryBase<Dispositivo>
{
    /// <summary>Dispositivos ativos de um Responsável — é por eles que o push sai.</summary>
    Task<IEnumerable<Dispositivo>> ObterAtivosDoTutorAsync(Guid tutorId);

    /// <summary>
    /// Busca pelo push token. Reinstalar o app devolve o mesmo token do fabricante,
    /// e o registro existente é reaproveitado em vez de duplicado.
    /// </summary>
    Task<Dispositivo?> ObterPorPushTokenAsync(string pushToken);
}
