using Vetly.Application.DTOs.Carteira;
using Vetly.Application.DTOs.Comum;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Histórico unificado da carteira do Responsável: pagamentos, resgates e reembolsos
/// numa linha do tempo só (RN-115).
/// </summary>
public interface ICarteiraService
{
    /// <summary>
    /// Página do histórico com filtros, ordenação (<see cref="CamposDeOrdenacao.Carteira"/>)
    /// e os totais do filtro. Escopo do token: o Responsável vê a própria carteira e o
    /// Admin, qualquer uma (RN-106).
    /// </summary>
    Task<HistoricoDaCarteiraDto> ObterHistoricoAsync(
        Guid tutorId, FiltroHistoricoDaCarteiraDto filtro, Paginacao paginacao, Ordenacao? ordenacao = null);
}
