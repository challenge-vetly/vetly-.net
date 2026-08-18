using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Factories;

/// <summary>Factory do calendário de obrigações para gatos (RN-069).</summary>
public class ObrigacaoFelinaFactory : IObrigacaoFactory
{
    /// <inheritdoc/>
    public bool Aplicavel(string especie) => especie.Equals("Felino", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public IEnumerable<ObrigacaoDoPet> GerarCalendario(Guid animalId, DateTime dataCadastro) =>
    [
        new ObrigacaoDoPet(animalId, TipoObrigacao.Vacina, dataCadastro.AddDays(21)),
        new ObrigacaoDoPet(animalId, TipoObrigacao.Vermifugo, dataCadastro.AddDays(60)),
        new ObrigacaoDoPet(animalId, TipoObrigacao.CheckUp, dataCadastro.AddMonths(6)),
        new ObrigacaoDoPet(animalId, TipoObrigacao.Retorno, dataCadastro.AddMonths(12))
    ];
}
