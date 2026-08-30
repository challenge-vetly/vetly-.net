using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.ListaEspera;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato da lista de espera (RN-004/RN-037).</summary>
public interface IListaEsperaService
{
    /// <summary>Coloca o Responsável na fila de um veterinário.</summary>
    Task<ItemListaEsperaDto> EntrarAsync(EntrarNaListaDto dto);

    /// <summary>Pedidos de um Responsável.</summary>
    Task<IEnumerable<ItemListaEsperaDto>> ObterDoTutorAsync(Guid tutorId);

    /// <summary>Sai da fila.</summary>
    Task SairAsync(Guid id);

    /// <summary>
    /// Aceita a vaga oferecida e segue para o checkout, no mesmo caminho do fluxo
    /// normal (RN-037). Prioridade vencida devolve 409.
    /// </summary>
    Task<CheckoutCriadoDto> ConfirmarVagaAsync(Guid id, Guid servicoId);

    /// <summary>
    /// Oferece um horário liberado ao primeiro da fila, com prioridade de 15 minutos
    /// (RN-037). Devolve nulo quando não há ninguém esperando.
    /// </summary>
    Task<ItemListaEsperaDto?> PromoverProximoAsync(Guid slotId);
}
