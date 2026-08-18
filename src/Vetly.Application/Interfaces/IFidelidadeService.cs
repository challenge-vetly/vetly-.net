using Vetly.Application.DTOs.Fidelidade;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de fidelidade (RN-070..075).</summary>
public interface IFidelidadeService
{
    /// <summary>
    /// Pontua uma consulta realizada (RN-070/075): se cumprir uma obrigação pendente no
    /// prazo, marca-a como cumprida e concede pontos cheios; senão, pontua como avulsa
    /// (peso menor). Sempre recalcula o tier do responsável em seguida.
    /// </summary>
    Task PontuarConsultaRealizadaAsync(Guid consultaId, Guid animalId, Guid responsavelId, TipoServico tipoServico, DateTime agora);

    /// <summary>
    /// Estorna o lançamento de pontos originado por uma consulta cancelada/reembolsada, se
    /// houver, e recalcula o tier (RN-075). No-op se a consulta não tiver pontuado.
    /// </summary>
    Task EstornarPontosPorCancelamentoAsync(Guid consultaId, DateTime agora);

    /// <summary>
    /// Calcula o desconto de fidelidade para um valor de serviço, pelo tier do
    /// responsável (RN-071/072). Zerado se o responsável estiver sob penalidade de
    /// no-show (RN-064), mesmo com tier elegível.
    /// </summary>
    Task<ResultadoDescontoFidelidadeDto> CalcularDescontoAsync(Guid responsavelId, decimal valorServico, DateTime agora);

    /// <summary>Retorna o resumo de fidelidade (tier, saldo, progresso) de um responsável.</summary>
    Task<FidelidadeDto> ObterFidelidadeAsync(Guid responsavelId);

    /// <summary>Retorna o extrato completo de lançamentos de pontos de um responsável.</summary>
    Task<IEnumerable<PontosFidelidadeDto>> ObterExtratoAsync(Guid responsavelId);
}
