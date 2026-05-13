using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Contrato de repositório específico para a entidade <see cref="Veterinario"/>.
/// Estende o repositório base com buscas específicas do domínio.
/// </summary>
public interface IVeterinarioRepository : IRepositoryBase<Veterinario>
{
    /// <summary>Busca um veterinário pelo valor do CRMV (ex: "12345-SP").</summary>
    Task<Veterinario?> ObterPorCrmvAsync(string crmv);

    /// <summary>Retorna todos os veterinários ativos de uma determinada UF.</summary>
    Task<IEnumerable<Veterinario>> ObterPorUfAsync(string uf);

    /// <summary>Retorna consultas futuras agendadas para um veterinário (usado no soft delete — RN-008).</summary>
    Task<IEnumerable<Consulta>> ObterAgendaFuturaAsync(Guid veterinarioId);

    /// <summary>Retorna todos os veterinários ativos cadastrados na plataforma.</summary>
    Task<IEnumerable<Veterinario>> ObterAtivosAsync();

    /// <summary>Retorna todos os veterinários vinculados a uma empresa.</summary>
    Task<IEnumerable<Veterinario>> ObterPorEmpresaAsync(Guid empresaId);
}
