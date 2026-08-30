using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório da lista de espera (RN-004/RN-037).</summary>
public interface IListaEsperaRepository
{
    Task<ItemListaEspera?> ObterPorIdAsync(Guid id);

    /// <summary>Pedidos de um Responsável, do mais recente para o mais antigo.</summary>
    Task<IEnumerable<ItemListaEspera>> ObterDoTutorAsync(Guid tutorId);

    /// <summary>Pedido em espera de um animal na fila de um veterinário, se houver.</summary>
    Task<ItemListaEspera?> ObterAguardandoDoAnimalAsync(Guid animalId, Guid veterinarioId);

    /// <summary>
    /// Primeiro da fila de um veterinário — a ordem é a data de entrada (RN-037).
    /// </summary>
    Task<ItemListaEspera?> ObterPrimeiroAguardandoAsync(Guid veterinarioId);

    /// <summary>Pedidos notificados cuja janela de prioridade já venceu (RN-037).</summary>
    Task<IEnumerable<ItemListaEspera>> ObterNotificadosVencidosAsync(Guid veterinarioId, DateTime agora);

    Task AdicionarAsync(ItemListaEspera item);
    void Atualizar(ItemListaEspera item);
    Task<int> SalvarAsync();
}
