using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>
/// Factory de fallback do calendário de obrigações para espécies sem protocolo dedicado
/// (RN-069). Sempre aplicável — deve ser a última a ser verificada pelo ObrigacaoService.
/// </summary>
public class ObrigacaoGenericaFactory : IObrigacaoFactory
{
    /// <inheritdoc/>
    public bool Aplicavel(string especie) => true;

    /// <inheritdoc/>
    public IEnumerable<ObrigacaoDoPet> GerarCalendario(Guid animalId, DateTime dataCadastro) =>
    [
        new ObrigacaoDoPet(animalId, TipoObrigacao.Vacina, dataCadastro.AddDays(45)),
        new ObrigacaoDoPet(animalId, TipoObrigacao.CheckUp, dataCadastro.AddMonths(12))
    ];
}
