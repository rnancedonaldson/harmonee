using Harmonee.Shared.Application.Interfaces;
using Harmonee.Shared.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.Shared.Infrastructure.Models;

public class Repository<T>(DbContext context) : IAsyncDisposable, IRepository<T> where T : class, IEntity
{
    private DbSet<T> _dbSet { get; init; } = context.Set<T>();

    public async Task Add(T entity, CancellationToken cancellationToken)
        => await _dbSet.AddAsync(entity, cancellationToken);

    public async Task AddRange(IEnumerable<T> entities, CancellationToken cancellationToken)
        => await _dbSet.AddRangeAsync(entities, cancellationToken);

    public async Task Delete(T entity, CancellationToken cancellationToken)
        => _dbSet.Remove(entity);

    public async Task Delete(Guid entityId, CancellationToken cancellationToken)
    {
        if (await GetById(entityId, cancellationToken) is T entity)
            await Delete(entity, cancellationToken);
    }

    public async Task<T?> FirstOrDefault(Func<T, bool> predicate, CancellationToken cancellationToken, T? defaultValue = null)
        => await _dbSet.SingleOrDefaultAsync(e => predicate(e), cancellationToken) ?? defaultValue;

    public async Task<T?> GetById(Guid id, CancellationToken cancellationToken)
        => await _dbSet.FindAsync(id, cancellationToken);

    public async Task<IEnumerable<T>> List(IEnumerable<Guid> ids, CancellationToken cancellationToken)
        => await _dbSet.Where(e => ids.Contains(e.Id)).ToListAsync(cancellationToken);

    public async Task<IEnumerable<T>> Search(Func<T, bool> predicate, CancellationToken cancellationToken)
        => await _dbSet.Where(e => predicate(e)).ToListAsync(cancellationToken);

    public async Task Update(T entity, CancellationToken cancellationToken)
        => _dbSet.Update(entity);

    public async Task Update(IEnumerable<T> entities, CancellationToken cancellationToken)
        => _dbSet.UpdateRange(entities);

    public async ValueTask DisposeAsync()
        => await context.SaveChangesAsync();
}
