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
}
