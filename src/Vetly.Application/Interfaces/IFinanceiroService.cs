using Vetly.Application.DTOs.Financeiro;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Consolidado financeiro e liquidação de repasses (RN-070/RN-071/RN-072).
/// </summary>
public interface IFinanceiroService
{
    /// <summary>
    /// Consolidado do período. Sem parâmetro, o mês corrente — o recorte do
    /// fechamento, e o que evita varrer a base inteira.
    /// </summary>
    Task<ConsolidadoFinanceiroDto> ObterConsolidadoAsync(DateTime? inicio, DateTime? fim);

    /// <summary>
    /// Marca os repasses do período como liquidados (RN-071). Já liquidado é
    /// ignorado: chamar o mesmo fechamento duas vezes não paga duas vezes.
    /// </summary>
    Task<LiquidacaoRealizadaDto> LiquidarAsync(LiquidarRepasseDto dto);
}
