using System.Linq.Expressions;

namespace Vetly.Application.Interfaces;

/// <summary>
/// Contrato genérico de repositório com operações CRUD básicas.
/// Toda entidade do domínio deve ter um repositório que implemente esta interface.
/// </summary>
/// <typeparam name="T">Tipo da entidade de domínio.</typeparam>
public interface IRepositoryBase<T> where T : class
{
    /// <summary>Retorna todas as entidades sem filtro.</summary>
    Task<IEnumerable<T>> ObterTodosAsync();

    /// <summary>Busca uma entidade pelo seu identificador único.</summary>
    Task<T?> ObterPorIdAsync(Guid id);

    /// <summary>Retorna entidades que satisfazem o predicado informado.</summary>
    Task<IEnumerable<T>> BuscarAsync(Expression<Func<T, bool>> predicado);

    /// <summary>Adiciona uma nova entidade ao contexto (pendente de SaveChanges).</summary>
    Task AdicionarAsync(T entidade);

    /// <summary>Atualiza os dados de uma entidade existente no contexto.</summary>
    void Atualizar(T entidade);

    /// <summary>Remove uma entidade do contexto (pendente de SaveChanges).</summary>
    void Remover(T entidade);

    /// <summary>Persiste todas as mudanças pendentes no banco de dados.</summary>
    Task<int> SalvarAsync();
}
