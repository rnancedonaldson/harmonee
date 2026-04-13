using Harmonee.Shared.Application.Interfaces;
using Harmonee.Shared.Domain.Interfaces;
using Harmonee.Shared.Infrastructure.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Harmonee.Shared.Infrastructure.Extensions;

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

    public static IServiceCollection RegisterHandlers(this IServiceCollection services)
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        var handlers = GetAllHandlers(types);

        foreach (var handler in handlers)
            services.AddScoped(handler.ServiceType, handler.ImplementationType);

        return services;
    }

    public static IApplicationBuilder MapEndpoints(this IApplicationBuilder app)
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        var handlers = GetAllHandlers(types);
        var requests = GetAllRequests(types);

        var handlerRoutes = handlers.Join(requests,
            h => h.RequestType,
            r => r.RequestType,
            (h, r) => new { })


    }

    private static IEnumerable<RequestRegistration> GetAllRequests(Type[] types)
        => GetCommands(types).Concat(GetQueries(types));

    private static IEnumerable<RequestRegistration> GetCommands(Type[] types)
        => types.Where(t => t.IsAssignableTo(typeof(ICommand<IResource, IResult>)))
            .Select(t =>
            {
                var commandInterface = t.GetInterface(nameof(ICommand<IResource, IResult>));
                var resourceType = commandInterface!.GetGenericArguments()[0];
                var resultType = commandInterface.GetGenericArguments()[1];
                var route = (string)t.GetProperty("Route", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)!;
                return new RequestRegistration(t, resourceType, resultType, route);
            });

    private static IEnumerable<RequestRegistration> GetQueries(Type[] types)
        => types.Where(t => t.IsAssignableTo(typeof(IQuery<IResource, IResult>)))
            .Select(t =>
            {
                var queryInterface = t.GetInterface(nameof(IQuery<IResource, IResult>));
                var resourceType = queryInterface!.GetGenericArguments()[0];
                var resultType = queryInterface.GetGenericArguments()[1];
                var route = (string)t.GetProperty("Route", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)!;
                return new RequestRegistration(t, resourceType, resultType, route);
            });

    private static IEnumerable<HandlerRegistration> GetAllHandlers(Type[] types)
        => GetCommandHandlers(types).Concat(GetQueryHandlers(types));

    private static IEnumerable<HandlerRegistration> GetCommandHandlers(Type[] types)
        => types.Where(t => t.IsAssignableTo(typeof(ICommandHandler<ICommand<IResource, IResult>>)))
            .Select(t =>
            {
                var commandHandlerInterface = t.GetInterface(nameof(ICommandHandler<ICommand<IResource, IResult>>));
                var requestType = commandHandlerInterface!.GetGenericArguments()[0];
                var resultType = commandHandlerInterface.GetGenericArguments()[1];
                return new HandlerRegistration(commandHandlerInterface, t, requestType);
            });

    private static IEnumerable<HandlerRegistration> GetQueryHandlers(Type[] types)
        => types.Where(t => t.IsAssignableTo(typeof(IQueryHandler<IQuery<IResource, IResult>>))).Select(t =>
            {
                var queryHandlerInterface = t.GetInterface(nameof(IQueryHandler<IQuery<IResource, IResult>>));
                var requestType = queryHandlerInterface!.GetGenericArguments()[0];
                var resultType = queryHandlerInterface.GetGenericArguments()[1];
                return new HandlerRegistration(queryHandlerInterface, t, requestType);
            });

    private record HandlerRegistration(Type ServiceType, Type ImplementationType, Type RequestType);
    private record RequestRegistration(Type RequestType, Type ResourceType, Type ResultType, string Route);
}
