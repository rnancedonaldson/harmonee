using Harmonee.Shared.Application.Interfaces;
using Harmonee.Shared.Application.Models;
using Harmonee.Shared.Domain.Interfaces;
using Harmonee.Shared.Infrastructure.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using IHResult = Harmonee.Shared.Application.Interfaces.IResult;

namespace Harmonee.Shared.Infrastructure.Extensions;

public static class DependencyInjection
{
    /// <summary>
    /// Uses reflection to register the dbcontext and automatically generate repositories for the available models.
    /// </summary>
    /// <typeparam name="T">The HarmoneeContext to register</typeparam>
    /// <param name="services">The service collection the context will be registered to</param>
    /// <returns></returns>
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

    /// <summary>
    /// Uses reflection to register all request handlers available to the executing assembly
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection RegisterHandlers(this IServiceCollection services)
    {
        foreach (var handler in GetAllHandlersFromAssembly())
            services.AddScoped(handler.ServiceType, handler.ImplementationType);

        return services;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    public static WebApplication MapEndpoints(this WebApplication app)
    {
        var requests = GetAllRequests();
        var registeredHandlers = GetAllHandlersFromApp(app);
        var requestHandlerMap = registeredHandlers
            .GroupBy(h => h.RequestType)
            .ToDictionary(g => g.Key, g => g.First());


        foreach (var request in requests)
        {
            if (!requestHandlerMap.TryGetValue(request.RequestType, out var handlerRegistration))
                continue;

            app.MapMethods(request.Route, [request.HttpMethod.ToString()], (IRequest request, HttpContext context) =>
            {   
                // For future- set up factories to request the exact handler for this request instead of the secondary pattern matching
                var scopedHandler = context.RequestServices.GetService(handlerRegistration.ImplementationType);
                if (request is Command<IResource, IHResult> command &&
                    scopedHandler is ICommandHandler<Command<IResource, IHResult>> commandHandler)
                    return commandHandler.HandleAsync(command);
                if (request is Query<IResource, IHResult> query &&
                    scopedHandler is IQueryHandler<Query<IResource, IHResult>> queryHandler)
                    return queryHandler.HandleAsync(query);
                else return Task.FromResult(new Result("Failure") as IHResult);
            });
        }

        return app;
    }

    private static IEnumerable<RequestRegistration> GetAllRequests()
        => GetAssemblyTypesByParentType([typeof(IRequest)])
            .Select(BuildRequestRegistration)
            .Where(r => r is not null).Select(r => r!);

    private static IEnumerable<Type> GetAssemblyTypesByParentType(Type[] parentTypes)
        => Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => parentTypes.Any(pt => t.IsAssignableTo(pt)));

    private static IEnumerable<Type> GetRegisteredTypesByParentType(this WebApplication app, Type[] parentTypes)
        => parentTypes.Select(pt => app.Services.GetServices(pt)?.FirstOrDefault()?.GetType())
            .Where(t => t is not null).Select(t => t!);

    private static IEnumerable<HandlerRegistration> GetAllHandlersFromAssembly()
        => GetAssemblyTypesByParentType([typeof(ICommandHandler<>), typeof(IQueryHandler<>)])
            .Select(BuildHandlerRegistration).Where(r => r is not null).Select(r => r!);

    private static IEnumerable<HandlerRegistration> GetAllHandlersFromApp(WebApplication app)
        => app.GetRegisteredTypesByParentType([typeof(ICommandHandler<>), typeof(IQueryHandler<>)])
            .Select(BuildHandlerRegistration).Where(r => r is not null).Select(r => r!);

    private static HandlerRegistration? BuildHandlerRegistration(Type handler)
    {
        var handlerType = handler.GetInterface(nameof(ICommandHandler<>)) ?? handler.GetInterface(nameof(IQueryHandler<>));
        var handlerArgTypes = handlerType?.GetGenericArguments();

        var requestType = handlerArgTypes?.ElementAtOrDefault(0);
        var resultType = handlerArgTypes?.ElementAtOrDefault(1);
        return handlerType is null || requestType is null || resultType is null ? null :
                new(handlerType, requestType, resultType);
    }

    private static RequestRegistration? BuildRequestRegistration(Type request)
    {
        var requestType = request.GetInterface(nameof(ICommand<IResource, Result>)) ?? 
            request.GetInterface(nameof(IQuery<IResource, Result>));
        var requestArgTypes = requestType?.GetGenericArguments();

        var resourceType = requestArgTypes?.ElementAtOrDefault(0);
        var resultType = requestArgTypes?.ElementAtOrDefault(1);
        var route = requestType?.GetProperty("Route", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)?.ToString();
        var httpMethod = requestType?.GetProperty("Route", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as HttpMethod;

        return requestType is null || resourceType is null || resultType is null || route is null || httpMethod is null ? null :
            new RequestRegistration(requestType, resourceType, resultType, route, httpMethod);
    }

    internal record HandlerRegistration(Type ServiceType, Type ImplementationType, Type RequestType);
    internal record RequestRegistration(Type RequestType, Type ResourceType, Type ResultType, string Route, HttpMethod HttpMethod);
}
