using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Contrato de repositório específico para a entidade <see cref="Tutor"/>.</summary>
public interface ITutorRepository : IRepositoryBase<Tutor>
{
    /// <summary>Busca um tutor pelo endereço de e-mail.</summary>
    Task<Tutor?> ObterPorEmailAsync(string email);

    /// <summary>Retorna todos os tutores ativos cadastrados.</summary>
    Task<IEnumerable<Tutor>> ObterAtivosAsync();
}
