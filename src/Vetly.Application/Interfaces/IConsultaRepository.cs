using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade <see cref="Consulta"/>.
/// Suporta filtros compostos por data, veterinário e status.
/// </summary>
public interface IConsultaRepository : IRepositoryBase<Consulta>
{
    /// <summary>Retorna todas as consultas de um veterinário, opcionalmente filtradas por data.</summary>
    Task<IEnumerable<Consulta>> ObterPorVeterinarioAsync(Guid veterinarioId, DateTime? dataInicio = null, DateTime? dataFim = null);

    /// <summary>Retorna todas as consultas de um animal.</summary>
    Task<IEnumerable<Consulta>> ObterPorAnimalAsync(Guid animalId);

    /// <summary>Retorna consultas com filtros compostos (data, veterinário, status).</summary>
    Task<IEnumerable<Consulta>> ObterComFiltrosAsync(DateTime? dataInicio, DateTime? dataFim, Guid? veterinarioId, StatusConsulta? status);

    /// <summary>Indica se o veterinário já teve alguma consulta com este animal (base do acesso restrito clássico — RN-010).</summary>
    Task<bool> ExisteConsultaAsync(Guid veterinarioId, Guid animalId);

    /// <summary>Retorna todas as consultas de um conjunto de veterinários — usado no dashboard consolidado da empresa (RN-007).</summary>
    Task<IEnumerable<Consulta>> ObterPorVeterinariosAsync(IEnumerable<Guid> veterinarioIds);
}
