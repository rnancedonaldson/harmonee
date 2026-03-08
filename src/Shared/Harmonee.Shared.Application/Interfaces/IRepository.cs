using System;
using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.Shared.Application.Interfaces;

public interface IRepository<T> where T : class, IEntity
{
    public Task<T?> GetById(Guid id);
    public Task<IEnumerable<T>> List(IEnumerable<Guid> ids);
    public Task<IEnumerable<T>> Search(Func<T, bool> predicate);
    public Task<T?> FirstOrDefault(Func<T, bool> predicate, T? defaultValue = default(T));
    public Task Add(T entity);
    public Task Update(T entity);
    public Task Delete(T entity);
    public Task Delete(Guid entityId);
}
