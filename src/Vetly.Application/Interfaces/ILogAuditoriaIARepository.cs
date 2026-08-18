using Vetly.Domain.Entities;
using Vetly.Domain.Enums;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório para <see cref="LogAuditoriaIA"/>.</summary>
public interface ILogAuditoriaIARepository : IRepositoryBase<LogAuditoriaIA>
{
    /// <summary>Retorna toda a trilha de auditoria de IA de uma consulta, mais recente primeiro.</summary>
    Task<IEnumerable<LogAuditoriaIA>> ObterPorConsultaAsync(Guid consultaId);

    /// <summary>Retorna o log pendente (sem decisão) mais recente da consulta para o tipo informado.</summary>
    Task<LogAuditoriaIA?> ObterPendenteAsync(Guid consultaId, TipoSugestaoIA tipoSugestao);
}
