using Vetly.Application.DTOs.Internacao;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de internações.</summary>
public interface IInternacaoService
{
    Task<IEnumerable<InternacaoDto>> ObterTodosAsync();
    Task<InternacaoDto> ObterPorIdAsync(Guid id);
    Task<InternacaoDto> AbrirAsync(CriarInternacaoDto dto);
    /// <summary>Registra procedimentos diarios e acumula o valor total apurado (RN-100).</summary>
    Task<InternacaoDto> RegistrarProcedimentosAsync(Guid id, RegistrarProcedimentosDto dto);

    /// <summary>Encerra a internacao, apura o saldo restante apos desconto da caucao.</summary>
    Task<AltaInternacaoDto> DarAltaAsync(Guid id);
}
