using Vetly.Application.DTOs.Internacao;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de internações.</summary>
public interface IInternacaoService
{
    Task<IEnumerable<InternacaoDto>> ObterTodosAsync();
    Task<InternacaoDto> ObterPorIdAsync(Guid id);
    Task<InternacaoDto> AbrirAsync(CriarInternacaoDto dto);
    Task AtualizarAsync(Guid id, string procedimentosJson);

    /// <summary>Encerra a internação, apura o valor total e gera a Nota Fiscal.</summary>
    Task<InternacaoDto> DarAltaAsync(Guid id);
}
