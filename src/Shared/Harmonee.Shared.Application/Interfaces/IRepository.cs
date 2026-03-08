using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.Shared.Application.Interfaces;

public interface IRepository<T> where T : class, IEntity
{
    public Task<T?> GetById(Guid id, CancellationToken cancellationToken);
    public Task<IEnumerable<T>> List(IEnumerable<Guid> ids, CancellationToken cancellationToken);
    public Task<IEnumerable<T>> Search(Func<T, bool> predicate, CancellationToken cancellationToken);
    public Task<T?> FirstOrDefault(Func<T, bool> predicate, CancellationToken cancellationToken, T? defaultValue = default(T));
    public Task Add(T entity, CancellationToken cancellationToken);
    public Task Update(T entity, CancellationToken cancellationToken);
    public Task Delete(T entity, CancellationToken cancellationToken);
    public Task Delete(Guid entityId, CancellationToken cancellationToken);
}
