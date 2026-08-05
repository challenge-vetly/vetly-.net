using Vetly.Application.DTOs.IA;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Orquestra a IA dentro do ciclo de vida da consulta (RN-096..100): busca o contexto
/// clínico, chama o <see cref="IOllamaService"/>, grava a trilha de auditoria
/// (<see cref="Vetly.Domain.Entities.LogAuditoriaIA"/>) e aplica a decisão do veterinário
/// ao estado final da consulta.
/// </summary>
public interface IConsultaIaService
{
    /// <summary>Sugere hipóteses diagnósticas a partir do contexto acessível ao vet naquela consulta (RN-096.1).</summary>
    Task<SugestaoDiagnosticoResponseDto> SugerirDiagnosticoAsync(Guid consultaId);

    /// <summary>
    /// Sugere protocolo de tratamento. Recusa com IA-001 antes de chamar o modelo se o
    /// peso do animal não estiver cadastrado (RN-096.2).
    /// </summary>
    Task<SugestaoProtocoloResponseDto> SugerirProtocoloAsync(Guid consultaId);

    /// <summary>Registra a decisão do veterinário sobre a sugestão pendente, finalizando o log (RN-099).</summary>
    Task<RegistrarDecisaoIAResponseDto> RegistrarDecisaoAsync(Guid consultaId, RegistrarDecisaoIADto dto);

    /// <summary>Retorna a trilha de auditoria completa de IA da consulta (RN-098).</summary>
    Task<IEnumerable<LogAuditoriaIADto>> ObterAuditoriaAsync(Guid consultaId);
}
