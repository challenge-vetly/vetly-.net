using Vetly.Application.DTOs.Internacao;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de internações.</summary>
public interface IInternacaoService
{
    Task<IEnumerable<InternacaoDto>> ObterTodosAsync();
    Task<InternacaoDto> ObterPorIdAsync(Guid id);
    Task<InternacaoDto> AbrirAsync(CriarInternacaoDto dto);
    Task AtualizarAsync(Guid id, string procedimentosJson);

    /// <summary>Encerra a internacao, apura o saldo restante apos desconto da caucao.</summary>
    Task<AltaInternacaoDto> DarAltaAsync(Guid id);
}
