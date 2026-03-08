using Harmonee.Shared.Application.Interfaces;
using Harmonee.Shared.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Harmonee.Shared.Presentation.Extensions;

public static class DependencyInjection
{
    public static IServiceCollection RegisterHarmoneeContext<T>(this IServiceCollection services) where T : HarmoneeContext
    {
        services.AddDbContext<T>();

        // Identify all DbSet properties
        var contextType = typeof(T);
        var dbSetProperties = contextType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType && 
                        p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>));
        
        // Reflection wizardry- get the entity type from the dbsets and register that as a repository and readonly repository
        foreach (var prop in dbSetProperties)
        {
            var entityType = prop.PropertyType.GetGenericArguments()[0];
            services.AddScoped(typeof(IRepository<>).MakeGenericType(entityType), typeof(Repository<>).MakeGenericType(entityType));
            services.AddScoped(typeof(IReadOnlyRepository<>).MakeGenericType(entityType), typeof(ReadOnlyRepository<>).MakeGenericType(entityType));
        }

        return services;
    }
}
