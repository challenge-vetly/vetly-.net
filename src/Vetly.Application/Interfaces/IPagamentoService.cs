using Vetly.Application.DTOs.Pagamento;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de pagamentos.</summary>
public interface IPagamentoService
{
    Task<IEnumerable<PagamentoDto>> ObterTodosAsync();
    Task<PagamentoDto> ObterPorIdAsync(Guid id);
    Task<PagamentoDto> CriarAsync(CriarPagamentoDto dto);
    Task<PagamentoDto> ProcessarSplitAsync(Guid id);

    /// <summary>
    /// Simula o pagamento de uma consulta: cria o pagamento (sempre "sucesso"), calcula a
    /// comissão pelo plano do veterinário (RN-089) e confirma a consulta (RN-037/058).
    /// </summary>
    Task<SimularPagamentoResponseDto> ProcessarSimuladoAsync(SimularPagamentoDto dto);
}
