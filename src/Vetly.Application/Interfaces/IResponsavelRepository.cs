using Vetly.Domain.Entities;

namespace Vetly.Application.Interfaces;

/// <summary>Contrato de repositório específico para a entidade <see cref="Responsavel"/>.</summary>
public interface IResponsavelRepository : IRepositoryBase<Responsavel>
{
    /// <summary>Busca um responsavel pelo endereço de e-mail.</summary>
    Task<Responsavel?> ObterPorEmailAsync(string email);

    /// <summary>Retorna todos os responsaveis ativos cadastrados.</summary>
    Task<IEnumerable<Responsavel>> ObterAtivosAsync();
}
