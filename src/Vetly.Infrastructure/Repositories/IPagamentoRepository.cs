using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Contrato de repositório específico para a entidade <see cref="Pagamento"/>.</summary>
public interface IPagamentoRepository : IRepositoryBase<Pagamento>
{
    /// <summary>Retorna todos os pagamentos de um tutor.</summary>
    Task<IEnumerable<Pagamento>> ObterPorTutorAsync(Guid tutorId);

    /// <summary>Retorna o pagamento vinculado a uma consulta, se existir.</summary>
    Task<Pagamento?> ObterPorConsultaAsync(Guid consultaId);
}
