using Vetly.Application.DTOs.Obrigacao;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço do calendário de obrigações do pet (RN-069).</summary>
public interface IObrigacaoService
{
    /// <summary>Gera o calendário de obrigações do animal via Factory por espécie (RN-069).</summary>
    Task<IEnumerable<ObrigacaoDoPetDto>> GerarCalendarioAsync(Guid animalId);

    /// <summary>Lista as obrigações de um animal, com o status "atrasada" derivado.</summary>
    Task<IEnumerable<ObrigacaoDoPetDto>> ObterPorAnimalAsync(Guid animalId);
}
