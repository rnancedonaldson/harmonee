using System;
using Harmonee.Shared.Application.Interfaces;
using Harmonee.Shared.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.Shared.Infrastructure.Models;

public class ReadOnlyRepository<T>(DbSet<T> dbSet) : IReadOnlyRepository<T> where T : class, IEntity
{
    public async Task<T?> FirstOrDefault(IEntityFilter<T> filter, T? defaultValue = default)
        => await dbSet.AsNoTracking().FirstOrDefaultAsync(e => filter.Allows(e)) ?? defaultValue;
    
    public async Task<T?> GetById(Guid id)
        => await dbSet.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

    public async Task<IEnumerable<T>> List(IEnumerable<Guid> ids)
        => await dbSet.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync();

    public async Task<IEnumerable<T>> Search(IEntityFilter<T> filter) 
        => await dbSet.AsNoTracking().Where(e => filter.Allows(e)).ToListAsync();
}
