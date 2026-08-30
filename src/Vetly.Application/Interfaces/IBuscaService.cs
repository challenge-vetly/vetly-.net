using Vetly.Application.DTOs.Busca;
using Vetly.Application.DTOs.Comum;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato do matching por geolocalização (RN-001 a RN-033).
/// </summary>
public interface IBuscaService
{
    /// <summary>
    /// Lista os prestadores elegíveis dentro do raio, ordenados pelo score de
    /// distância, avaliação e disponibilidade (RN-030).
    /// </summary>
    Task<ResultadoBuscaDto> BuscarAsync(FiltroBuscaDto filtro, Paginacao paginacao);
}
