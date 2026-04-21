using Harmonee.Shared.Application.Interfaces;
using Harmonee.Shared.Domain.Interfaces;
using Harmonee.Shared.Infrastructure.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var types = Assembly.GetExecutingAssembly().GetTypes();
        var handlers = GetAllHandlers(types);
        if (!handlers.Any())
            return app;

        var requestHandlerMap = handlers.ToDictionary(h => h.RequestType);
        var requests = GetAllRequests(types);

        var endpoints = requests.Join(requestHandlerMap,
            r => r.RequestType,
            h => h.Key,
            (r, h) => new { Route = r.Route, Method = r.HttpMethod, Handler = h.Value });
        
        foreach (var endpoint in endpoints)
        {
            Func<string, RouteHandlerBuilder> mapMethod = endpoint.Method.ToString() switch
            {
                nameof(HttpMethod.Get) => (string route) => app.MapGet(endpoint.Route, (var ctx) => Task.CompletedTask),
                nameof(HttpMethod.Post) => (string route) => app.MapPost(endpoint.Route, (var ctx) => Task.CompletedTask),
                nameof(HttpMethod.Put) => (string route) => app.MapPut(endpoint.Route, (var ctx) => Task.CompletedTask),
                nameof(HttpMethod.Delete) => (string route) => app.MapDelete(endpoint.Route, (var ctx) => Task.CompletedTask),
                _ => throw new NotSupportedException($"HTTP method {endpoint.Method} is not supported.")
            };
        }

        foreach (var request in requests)
        {
            var handler = requestHandlerMap.GetValueOrDefault(request.RequestType);
            if (handler is not null)
                app.MapGet("wtf/bro", (var ctx) => Task.CompletedTask);
        }

        var handlerRoutes = handlers.Join(requests,
            h => h.RequestType,
            r => r.RequestType,
            (h, r) => new { })


    }

    private static IEnumerable<RequestRegistration> GetAllRequests(Type[] types)
    {
        foreach (var type in types)
    {
            var commandInterface = type.GetInterface(nameof(ICommand<IResource, IResult>));
            var queryInterface = type.GetInterface(nameof(IQuery<IResource, IResult>));
            var requestInterface = type.GetInterface(nameof(IRequest<IResource>));

            if ((commandInterface is not null || queryInterface is not null) && requestInterface is not null)
            {
                var requestRegistration = BuildRequestRegistration(commandInterface?.GetGenericArguments() ?? queryInterface?.GetGenericArguments(), requestInterface);
                if (requestRegistration is not null)
                    yield return requestRegistration;
            }
        }
    }

    private static RequestRegistration? BuildRequestRegistration(Type[]? queryCommandArgs, Type? requestInterface)
    {
        if (queryCommandArgs?.Length != 2 || requestInterface is null)
            return null;

        var resourceType = queryCommandArgs.ElementAtOrDefault(0);
        var resultType = queryCommandArgs.ElementAtOrDefault(1);
        var route = requestInterface.GetProperty("Route", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)?.ToString();
        var httpMethod = requestInterface.GetProperty("Route", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as HttpMethod;
        if (resourceType is null || resultType is null || route is null || httpMethod is null)
            return null;

        return new RequestRegistration(requestInterface, resourceType, resultType, route, httpMethod);
    }

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
    private record RequestRegistration(Type RequestType, Type ResourceType, Type ResultType, string Route, HttpMethod HttpMethod);
}
