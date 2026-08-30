using Vetly.Application.DTOs.Consulta;
using Vetly.Application.DTOs.Veterinario;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato do serviço de veterinários.</summary>
public interface IVeterinarioService
{
    Task<IEnumerable<VeterinarioDto>> ObterTodosAsync();
    Task<VeterinarioDto> ObterPorIdAsync(Guid id);
    Task<IEnumerable<VeterinarioDto>> ObterPorRegiaoAsync(string uf);
    Task<IEnumerable<ConsultaDto>> ObterAgendaAsync(Guid veterinarioId);
    Task<VeterinarioDto> CriarAsync(CriarVeterinarioDto dto);
    Task AtualizarAsync(Guid id, CriarVeterinarioDto dto);

    /// <summary>Soft delete — retorna agendamentos futuros afetados (RN-022/RN-025).</summary>
    Task<IEnumerable<ConsultaDto>> DesativarAsync(Guid id);
}
