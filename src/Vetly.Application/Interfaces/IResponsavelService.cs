using Vetly.Application.DTOs.Animal;
using Vetly.Application.DTOs.Responsavel;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de responsaveis.</summary>
public interface IResponsavelService
{
    Task<IEnumerable<ResponsavelDto>> ObterTodosAsync();
    Task<ResponsavelDto> ObterPorIdAsync(Guid id);
    Task<IEnumerable<AnimalDto>> ObterAnimaisAsync(Guid responsavelId);
    Task<ResponsavelDto> CriarAsync(CriarResponsavelDto dto);
    Task AtualizarAsync(Guid id, CriarResponsavelDto dto);
    Task DesativarAsync(Guid id);

    /// <summary>Lista o histórico completo de consentimentos LGPD do responsável (RN-044, RN-086).</summary>
    Task<IEnumerable<ConsentimentoLgpdDto>> ListarConsentimentosAsync(Guid responsavelId);

    /// <summary>Concede um novo consentimento para a finalidade informada (RN-041/042/043).</summary>
    Task<ConsentimentoLgpdDto> ConcederConsentimentoAsync(Guid responsavelId, ConcederConsentimentoDto dto);

    /// <summary>Revoga o consentimento ativo da finalidade informada, preservando o histórico (RN-044).</summary>
    Task<ConsentimentoLgpdDto> RevogarConsentimentoAsync(Guid responsavelId, FinalidadeConsentimento finalidade);
}
