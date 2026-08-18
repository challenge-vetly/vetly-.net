using Vetly.Application.DTOs.Empresa;
using Vetly.Application.DTOs.Veterinario;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de empresas.</summary>
public interface IEmpresaService
{
    Task<IEnumerable<EmpresaDto>> ObterTodosAsync();
    Task<EmpresaDto> ObterPorIdAsync(Guid id);
    Task<EmpresaDto> CriarAsync(CriarEmpresaDto dto);
    Task AtualizarAsync(Guid id, CriarEmpresaDto dto);
    Task DesativarAsync(Guid id);
    Task<IEnumerable<VeterinarioDto>> ObterVeterinariosAsync(Guid empresaId);
    Task VincularVeterinarioAsync(Guid empresaId, Guid veterinarioId);

    /// <summary>
    /// Dashboard financeiro consolidado da empresa (RN-007): produção agregada dos vets
    /// vinculados, sem nenhum dado bancário pessoal ou remuneração individual. Restrito ao
    /// Admin da própria empresa (RN-001..006) — <c>ForbiddenException("ACESSO-002")</c> em
    /// tentativa de acesso cruzado.
    /// </summary>
    Task<DashboardConsolidadoDto> ObterDashboardConsolidadoAsync(Guid empresaId);

    /// <summary>Estado atual da assinatura Enterprise por faixa de nº de vets (RN-092).</summary>
    Task<AssinaturaEmpresaDto> ObterAssinaturaAsync(Guid empresaId);
}
