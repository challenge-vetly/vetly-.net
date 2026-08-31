using Vetly.Application.DTOs.Captura;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Fecho documental da consulta: a decisão do veterinário sobre o rascunho da IA e o
/// prontuário escrito à mão (RN-082/RN-085, §7.3).
/// </summary>
public interface IProntuarioService
{
    /// <summary>
    /// Registra a decisão sobre o rascunho: aprovar, corrigir ou não aprovar (RN-082).
    /// Toda decisão vira registro append-only na trilha de auditoria.
    /// </summary>
    Task<DecisaoRegistradaDto> DecidirAsync(Guid consultaId, DecisaoDoProntuarioDto dto);

    /// <summary>
    /// Registra o prontuário escrito à mão, sem IA no caminho (RN-085).
    /// </summary>
    Task<DecisaoRegistradaDto> RegistrarManualAsync(Guid consultaId, ProntuarioManualDto dto);

    /// <summary>Trilha de auditoria de uma consulta (RN-082).</summary>
    Task<List<LogAuditoriaIaDto>> ObterAuditoriaAsync(Guid consultaId);
}
