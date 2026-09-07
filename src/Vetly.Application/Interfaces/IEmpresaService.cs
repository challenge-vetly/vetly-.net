using Vetly.Application.DTOs.Empresa;
using Vetly.Application.DTOs.Repasse;
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
    /// Conta de repasse da unidade, mascarada (§4.1).
    ///
    /// É a conta do estabelecimento, não a de nenhum profissional — a §7.3 veda ao
    /// administrador os dados bancários pessoais dos vets, e é por isso que a conta
    /// da empresa e a do vet moram em rotas diferentes, com donos diferentes.
    /// </summary>
    Task<DadosDeRepasseDto> ObterDadosDeRepasseAsync(Guid empresaId);

    /// <summary>Informa ou substitui a conta de repasse da unidade (§4.1).</summary>
    Task<DadosDeRepasseDto> DefinirDadosDeRepasseAsync(Guid empresaId, DefinirDadosDeRepasseDto dto);
}
