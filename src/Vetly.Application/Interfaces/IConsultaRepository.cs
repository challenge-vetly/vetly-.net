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

    /// <summary>
    /// Consultas realizadas de um Responsável desde uma data. É a base da lista de
    /// avaliações pendentes (RN-055).
    /// </summary>
    Task<IEnumerable<Consulta>> ObterRealizadasDoTutorDesdeAsync(Guid tutorId, DateTime desde);

    /// <summary>
    /// Quais dos animais informados foram atendidos por algum dos profissionais
    /// informados. É o recorte do alerta da régua (RN-095, §6.4).
    ///
    /// Recebe os <b>dois</b> conjuntos e filtra no banco de propósito. O caminho
    /// natural seria carregar as consultas de cada profissional e cruzar em memória —
    /// e isso leria o histórico inteiro da unidade para responder sobre meia dúzia de
    /// animais. Os dois conjuntos aqui são pequenos por construção: os animais vêm das
    /// réguas que esgotaram três tentativas, e os profissionais, de uma unidade.
    /// </summary>
    Task<HashSet<Guid>> ObterAnimaisAtendidosAsync(
        IEnumerable<Guid> veterinarioIds, IEnumerable<Guid> animalIds);
}
