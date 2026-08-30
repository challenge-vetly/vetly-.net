using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato de repositório específico para a entidade <see cref="Veterinario"/>.
/// Estende o repositório base com buscas específicas do domínio.
/// </summary>
public interface IVeterinarioRepository : IRepositoryBase<Veterinario>
{
    /// <summary>Busca um veterinário pelo valor do CRMV (ex: "12345-SP").</summary>
    Task<Veterinario?> ObterPorCrmvAsync(string crmv);

    /// <summary>
    /// Busca por e-mail de acesso. Inclui inativos de proposito: o vet desativado
    /// precisa conseguir entrar para pedir o extrato dos proprios atendimentos (RN-024).
    /// </summary>
    Task<Veterinario?> ObterPorEmailAsync(string email);

    /// <summary>Retorna todos os veterinários ativos de uma determinada UF.</summary>
    Task<IEnumerable<Veterinario>> ObterPorUfAsync(string uf);

    /// <summary>Retorna consultas futuras agendadas para um veterinário (usado no soft delete — RN-022/RN-025).</summary>
    Task<IEnumerable<Consulta>> ObterAgendaFuturaAsync(Guid veterinarioId);

    /// <summary>Retorna todos os veterinários ativos cadastrados na plataforma.</summary>
    Task<IEnumerable<Veterinario>> ObterAtivosAsync();

    /// <summary>Retorna todos os veterinários vinculados a uma empresa.</summary>
    Task<IEnumerable<Veterinario>> ObterPorEmpresaAsync(Guid empresaId);
}
