using System;
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

    public async Task Delete(Guid entityId)
    {
        if (await GetById(entityId) is T entity)
            await Delete(entity, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
        => await context.SaveChangesAsync();

    public async Task<T?> FirstOrDefault(Func<T, bool> predicate, CancellationToken cancellationToken, T? defaultValue = null)
        => await _dbSet.SingleOrDefaultAsync(e => predicate(e), cancellationToken) ?? defaultValue;

    public async Task<T?> GetById(Guid id)
        => await _dbSet.FindAsync(id);

    public async Task<IEnumerable<T>> List(IEnumerable<Guid> ids)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<T>> Search(Func<T, bool> predicate)
    {
        throw new NotImplementedException();
    }

    public async Task Update(T entity)
    {
        throw new NotImplementedException();
    }
}
