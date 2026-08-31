using Vetly.Application.DTOs.Redistribuicao;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Redistribuição de consultas quando o profissional sai ou fica indisponível
/// (RN-025).
/// </summary>
public interface IRedistribuicaoService
{
    /// <summary>
    /// Veterinários que poderiam assumir a consulta, ordenados pela proximidade do
    /// horário original. Espécie é eliminatória (RN-029).
    /// </summary>
    Task<IEnumerable<CandidatoARedistribuicaoDto>> SugerirCandidatosAsync(Guid consultaId);

    /// <summary>
    /// Passa a consulta ao novo profissional, mantendo pagamento e animal, libera o
    /// horário antigo e avisa o Responsável (RN-025/RN-092).
    /// </summary>
    Task<RedistribuicaoRealizadaDto> RedistribuirAsync(Guid consultaId, RedistribuirConsultaDto dto);
}
