using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Trilha de auditoria das decisões sobre conteúdo de IA (RN-082).
///
/// O contrato é deliberadamente <b>append-only</b>: não há atualizar nem remover. Um
/// registro que pode ser reescrito depois não prova que houve decisão humana, e é
/// exatamente isso que esta tabela existe para provar.
/// </summary>
public interface IAuditoriaIaRepository
{
    Task AdicionarAsync(LogAuditoriaIa registro);

    /// <summary>Decisões de uma consulta, da mais recente à mais antiga.</summary>
    Task<IEnumerable<LogAuditoriaIa>> ObterDaConsultaAsync(Guid consultaId);

    Task<int> SalvarAsync();
}
