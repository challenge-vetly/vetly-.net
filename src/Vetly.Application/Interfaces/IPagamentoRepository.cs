using Vetly.Application.DTOs.Comum;
using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório específico para a entidade <see cref="Pagamento"/>.</summary>
public interface IPagamentoRepository : IRepositoryBase<Pagamento>
{
    /// <summary>Retorna todos os pagamentos de um tutor.</summary>
    Task<IEnumerable<Pagamento>> ObterPorTutorAsync(Guid tutorId);

    /// <summary>
    /// Retorna uma página de pagamentos, do mais recente para o mais antigo (§2.3).
    /// <paramref name="tutorId"/> restringe ao escopo do Responsável (RN-106);
    /// nulo devolve todos, e só o Admin chega assim.
    /// </summary>
    Task<ResultadoPaginado<Pagamento>> ObterPaginadoAsync(Paginacao paginacao, Guid? tutorId = null);

    /// <summary>
    /// Busca pela referência do provedor. É por ela que o webhook encontra o pagamento.
    /// </summary>
    Task<Pagamento?> ObterPorReferenciaExternaAsync(string referenciaExterna);

    /// <summary>Retorna o pagamento vinculado a uma consulta, se existir.</summary>
    Task<Pagamento?> ObterPorConsultaAsync(Guid consultaId);

    /// <summary>
    /// Pagamentos confirmados num período. É a base do consolidado financeiro e da
    /// liquidação: cobrança pendente ou recusada não entra em fechamento (RN-071).
    /// </summary>
    Task<IEnumerable<Pagamento>> ObterConfirmadosNoPeriodoAsync(DateTime inicio, DateTime fim);
}
