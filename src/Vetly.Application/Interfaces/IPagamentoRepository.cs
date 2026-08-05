using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório específico para a entidade <see cref="Pagamento"/>.</summary>
public interface IPagamentoRepository : IRepositoryBase<Pagamento>
{
    /// <summary>Retorna todos os pagamentos de um responsavel.</summary>
    Task<IEnumerable<Pagamento>> ObterPorResponsavelAsync(Guid responsavelId);

    /// <summary>Retorna o pagamento vinculado a uma consulta, se existir.</summary>
    Task<Pagamento?> ObterPorConsultaAsync(Guid consultaId);
}
