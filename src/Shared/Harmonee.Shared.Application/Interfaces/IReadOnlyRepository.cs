using System;
using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.Shared.Application.Interfaces;

public interface IReadOnlyRepository<T> where T : IEntity
{
    public Task<T?> GetById(Guid id);
    public Task<IEnumerable<T>> List(IEnumerable<Guid> ids);
    public Task<IEnumerable<T>> Search(IEntityFilter<T> filter);
    public Task<T?> FirstOrDefault(IEntityFilter<T> filter, T? defaultValue = default(T));
}
