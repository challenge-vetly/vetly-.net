using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>Factory do calendário de obrigações para cães (RN-069).</summary>
public class ObrigacaoCaninaFactory : IObrigacaoFactory
{
    /// <inheritdoc/>
    public bool Aplicavel(string especie) => especie.Equals("Canino", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public IEnumerable<ObrigacaoDoPet> GerarCalendario(Guid animalId, DateTime dataCadastro) =>
    [
        new ObrigacaoDoPet(animalId, TipoObrigacao.Vacina, dataCadastro.AddDays(30)),
        new ObrigacaoDoPet(animalId, TipoObrigacao.Vermifugo, dataCadastro.AddDays(90)),
        new ObrigacaoDoPet(animalId, TipoObrigacao.CheckUp, dataCadastro.AddMonths(6)),
        new ObrigacaoDoPet(animalId, TipoObrigacao.Retorno, dataCadastro.AddMonths(12))
    ];
}
