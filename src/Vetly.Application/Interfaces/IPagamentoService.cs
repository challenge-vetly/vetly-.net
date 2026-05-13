using Vetly.Application.DTOs.Pagamento;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de pagamentos.</summary>
public interface IPagamentoService
{
    Task<IEnumerable<PagamentoDto>> ObterTodosAsync();
    Task<PagamentoDto> ObterPorIdAsync(Guid id);
    Task<PagamentoDto> CriarAsync(CriarPagamentoDto dto);
    Task<PagamentoDto> ProcessarSplitAsync(Guid id);
}
