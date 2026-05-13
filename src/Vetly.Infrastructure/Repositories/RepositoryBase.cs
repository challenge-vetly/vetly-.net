using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Vetly.Application.Interfaces;
using Vetly.Infrastructure.Data;

namespace Vetly.Infrastructure.Repositories;

/// <summary>
/// Implementação genérica de repositório usando EF Core.
/// Centraliza as operações CRUD para evitar duplicação em repositórios específicos.
/// </summary>
/// <typeparam name="T">Tipo da entidade de domínio.</typeparam>
public class RepositoryBase<T> : IRepositoryBase<T> where T : class
{
    /// <summary>Contexto EF Core injetado via construtor (DI).</summary>
    protected readonly VetlyDbContext _context;

    /// <summary>DbSet tipado para a entidade T — atalho para _context.Set&lt;T&gt;().</summary>
    protected readonly DbSet<T> _dbSet;

    public RepositoryBase(VetlyDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<T>> ObterTodosAsync() =>
        await _dbSet.ToListAsync();

    /// <inheritdoc/>
    public async Task<T?> ObterPorIdAsync(Guid id) =>
        await _dbSet.FindAsync(id);

    /// <inheritdoc/>
    public async Task<IEnumerable<T>> BuscarAsync(Expression<Func<T, bool>> predicado) =>
        await _dbSet.Where(predicado).ToListAsync();

    /// <inheritdoc/>
    public async Task AdicionarAsync(T entidade) =>
        await _dbSet.AddAsync(entidade);

    /// <inheritdoc/>
    public void Atualizar(T entidade) =>
        _dbSet.Update(entidade);

    /// <inheritdoc/>
    public void Remover(T entidade) =>
        _dbSet.Remove(entidade);

    /// <inheritdoc/>
    public async Task<int> SalvarAsync() =>
        await _context.SaveChangesAsync();
}
