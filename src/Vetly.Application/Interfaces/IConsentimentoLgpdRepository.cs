using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório para <see cref="ConsentimentoLgpd"/>.</summary>
public interface IConsentimentoLgpdRepository : IRepositoryBase<ConsentimentoLgpd>
{
    /// <summary>Retorna todo o histórico de consentimentos de um responsável, mais recentes primeiro.</summary>
    Task<IEnumerable<ConsentimentoLgpd>> ObterPorResponsavelAsync(Guid responsavelId);

    /// <summary>Retorna o registro ativo (ainda não revogado) mais recente para a finalidade, se houver.</summary>
    Task<ConsentimentoLgpd?> ObterAtivoAsync(Guid responsavelId, FinalidadeConsentimento finalidade);
}
