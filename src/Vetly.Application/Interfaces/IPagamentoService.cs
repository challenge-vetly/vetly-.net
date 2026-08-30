using Vetly.Application.DTOs.Comum;
using Vetly.Application.DTOs.Pagamento;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de pagamentos.</summary>
public interface IPagamentoService
{
    /// <summary>Lista pagamentos paginados (§2.3).</summary>
    Task<ResultadoPaginado<PagamentoDto>> ObterTodosAsync(Paginacao paginacao);
    Task<PagamentoDto> ObterPorIdAsync(Guid id);
    Task<PagamentoDto> CriarAsync(CriarPagamentoDto dto);
    Task<PagamentoDto> ProcessarSplitAsync(Guid id);
}
