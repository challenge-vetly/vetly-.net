using Vetly.Domain.Entities;

namespace Vetly.Application.Factories;

/// <summary>
/// Contrato do Factory Pattern para o calendário de obrigações do pet, gerado por espécie
/// no cadastro do animal (RN-069). O ObrigacaoService seleciona a factory correta via
/// IEnumerable&lt;IObrigacaoFactory&gt; — mesmo padrão de <see cref="IDocumentoFactory"/>.
/// </summary>
public interface IObrigacaoFactory
{
    /// <summary>Indica se esta factory sabe montar o calendário para a espécie informada.</summary>
    bool Aplicavel(string especie);

    /// <summary>Gera o calendário de obrigações a partir da data de cadastro do animal.</summary>
    IEnumerable<ObrigacaoDoPet> GerarCalendario(Guid animalId, DateTime dataCadastro);
}
