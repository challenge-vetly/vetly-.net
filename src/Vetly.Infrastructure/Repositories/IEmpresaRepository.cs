using Vetly.Domain.Entities;

namespace Vetly.Infrastructure.Repositories;

/// <summary>Contrato de repositório específico para a entidade <see cref="Empresa"/>.</summary>
public interface IEmpresaRepository : IRepositoryBase<Empresa>
{
    /// <summary>Retorna todas as empresas ativas cadastradas.</summary>
    Task<IEnumerable<Empresa>> ObterAtivasAsync();

    /// <summary>Retorna as empresas administradas por um veterinário.</summary>
    Task<IEnumerable<Empresa>> ObterPorAdministradorAsync(Guid veterinarioId);
}
