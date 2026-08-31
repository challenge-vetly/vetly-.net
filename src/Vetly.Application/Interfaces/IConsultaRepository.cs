using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Consulta;
using Vetly.Domain.Entities;

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

    /// <summary>
    /// Retorna uma página de consultas aplicando os filtros informados.
    /// A contagem total é feita sobre o filtro, antes do recorte da página.
    /// </summary>
    Task<ResultadoPaginado<Consulta>> ObterComFiltrosAsync(FiltroConsultaDto filtro, Paginacao paginacao);

    /// <summary>
    /// Consultas de um período, sem paginação. É a base do funil de atendimento
    /// (RN-106) — agregação precisa do conjunto inteiro, não de uma página.
    /// </summary>
    Task<IEnumerable<Consulta>> ObterNoPeriodoAsync(DateTime inicio, DateTime fim);
}
