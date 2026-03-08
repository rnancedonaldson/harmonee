using System;
using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.Shared.Application.Interfaces;

public interface IReadOnlyRepository<T> where T : IEntity
{
    public Task<T?> GetById(Guid id);
    public Task<IEnumerable<T>> List(IEnumerable<Guid> ids);
    public Task<IEnumerable<T>> Search(Func<T, bool> predicate);
    public Task<T?> FirstOrDefault(Func<T, bool> predicate, T? defaultValue = default(T));
}
