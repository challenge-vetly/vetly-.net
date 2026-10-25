using Microsoft.EntityFrameworkCore;
using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Consulta;
using Vetly.Application.Interfaces;
using Vetly.Domain.Entities;
using Vetly.Domain.Enums;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de <see cref="Consulta"/> com filtros compostos.
/// </summary>
public class ConsultaRepository : RepositoryBase<Consulta>, IConsultaRepository
{
    public ConsultaRepository(VetlyDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<IEnumerable<Consulta>> ObterPorVeterinarioAsync(
        Guid veterinarioId, DateTime? dataInicio = null, DateTime? dataFim = null)
    {
        var query = _dbSet.Where(c => c.VeterinarioId == veterinarioId);

        if (dataInicio.HasValue)
            query = query.Where(c => c.DataHora >= dataInicio.Value);

        if (dataFim.HasValue)
            query = query.Where(c => c.DataHora <= dataFim.Value);

        return await query.OrderBy(c => c.DataHora).ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Consulta>> ObterPorAnimalAsync(Guid animalId) =>
        await _dbSet
            .Where(c => c.AnimalId == animalId)
            .OrderByDescending(c => c.DataHora)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<ResultadoPaginado<Consulta>> ObterComFiltrosAsync(
        FiltroConsultaDto filtro, Paginacao paginacao, CriterioDeOrdenacao? ordenacao = null)
    {
        // Constrói a query dinamicamente — apenas os filtros informados são aplicados
        var query = _dbSet.AsQueryable();

        if (filtro.DataInicio.HasValue)
            query = query.Where(c => c.DataHora >= filtro.DataInicio.Value);

        if (filtro.DataFim.HasValue)
            query = query.Where(c => c.DataHora <= filtro.DataFim.Value);

        if (filtro.VeterinarioId.HasValue)
            query = query.Where(c => c.VeterinarioId == filtro.VeterinarioId.Value);

        if (filtro.TutorId.HasValue)
            query = query.Where(c => c.TutorId == filtro.TutorId.Value);

        if (filtro.AnimalId.HasValue)
            query = query.Where(c => c.AnimalId == filtro.AnimalId.Value);

        if (filtro.Status.HasValue)
            query = query.Where(c => c.Status == filtro.Status.Value);

        // Filtro legado, mantido enquanto dura a dupla escrita do StatusConsulta
        if (filtro.Cancelada.HasValue)
            query = query.Where(c => c.Cancelada == filtro.Cancelada.Value);

        // A contagem roda sobre o filtro inteiro, antes do recorte da pagina
        var total = await query.CountAsync();

        // A ordenacao entra no IQueryable antes do Skip/Take: ordenar depois de recortar
        // ordenaria so a pagina, e a pagina 2 poderia repetir ou pular registros.
        var itens = await Ordenar(query, ordenacao ?? PadraoConsultas)
            .Skip(paginacao.Deslocamento)
            .Take(paginacao.Tamanho)
            .ToListAsync();

        return new ResultadoPaginado<Consulta>(itens, total, paginacao);
    }

    /// <summary>Data e hora decrescente: o que a listagem sempre devolveu.</summary>
    private static readonly CriterioDeOrdenacao PadraoConsultas =
        new(CamposDeOrdenacao.Consultas.DataHora, Descendente: true);

    /// <summary>
    /// Traduz o campo da whitelist em expressao, num switch fechado.
    ///
    /// O desempate por <c>Id</c> nao e enfeite: com dois registros no mesmo valor de
    /// ordenacao, o banco pode devolve-los em ordem diferente a cada execucao, e o
    /// mesmo registro aparece na pagina 1 e na 2 enquanto outro some. Com uma chave
    /// unica no fim, a ordem e total e a paginacao fica estavel.
    /// </summary>
    private static IQueryable<Consulta> Ordenar(IQueryable<Consulta> query, CriterioDeOrdenacao criterio)
    {
        var ordenada = (criterio.Campo, criterio.Descendente) switch
        {
            (CamposDeOrdenacao.Consultas.Status, false) =>
                query.OrderBy(c => c.Status).ThenByDescending(c => c.DataHora),
            (CamposDeOrdenacao.Consultas.Status, true) =>
                query.OrderByDescending(c => c.Status).ThenByDescending(c => c.DataHora),
            (_, false) => query.OrderBy(c => c.DataHora),
            (_, true) => query.OrderByDescending(c => c.DataHora)
        };

        return ordenada.ThenBy(c => c.Id);
    }

/// <inheritdoc/>
    public async Task<HashSet<Guid>> ObterAnimaisAtendidosAsync(
        IEnumerable<Guid> veterinarioIds, IEnumerable<Guid> animalIds)
    {
        var vets = veterinarioIds.Distinct().ToList();
        var animais = animalIds.Distinct().ToList();

        // Conjunto vazio de qualquer um dos lados nao tem o que cruzar, e um IN () vazio
        // e SQL invalido no Oracle.
        if (vets.Count == 0 || animais.Count == 0)
            return [];

        // Distinct antes do ToListAsync: o interesse e a existencia do atendimento, e
        // trazer uma linha por consulta repetiria o mesmo animal dezenas de vezes.
        var atendidos = await _dbSet
            .Where(c => vets.Contains(c.VeterinarioId) && animais.Contains(c.AnimalId))
            .Select(c => c.AnimalId)
            .Distinct()
            .ToListAsync();

        return [.. atendidos];
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Consulta>> ObterRealizadasDoTutorDesdeAsync(Guid tutorId, DateTime desde) =>
        await _dbSet
            .AsNoTracking()
            .Where(c => c.TutorId == tutorId
                        && c.Status == StatusConsulta.Realizada
                        && c.DataHora >= desde)
            .OrderByDescending(c => c.DataHora)
            .ToListAsync();

    /// <inheritdoc/>
    public async Task<IEnumerable<Consulta>> ObterNoPeriodoAsync(DateTime inicio, DateTime fim) =>
        // Sem paginacao de proposito: agregacao precisa do conjunto inteiro, e uma
        // pagina daria um funil que muda conforme o tamanho da pagina (RN-106).
        await _dbSet
            .AsNoTracking()
            .Where(c => c.DataHora >= inicio && c.DataHora <= fim)
            .ToListAsync();
}
