using Vetly.Application.DTOs.Analytics;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Métricas agregadas da plataforma (RN-106).
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Funil de atendimento, uso da IA e receita do período. Sem parâmetro, os
    /// últimos 30 dias — a janela em que uma métrica ainda reage ao que foi mudado.
    /// </summary>
    Task<AnalyticsDaPlataformaDto> ObterDaPlataformaAsync(DateTime? inicio, DateTime? fim);
}
