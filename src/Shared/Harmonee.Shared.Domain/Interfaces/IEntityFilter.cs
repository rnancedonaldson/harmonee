using System;

namespace Harmonee.Shared.Domain.Interfaces;

public interface IEntityFilter<T> where T : IEntity
{
    public bool Allows(T entity);
}
