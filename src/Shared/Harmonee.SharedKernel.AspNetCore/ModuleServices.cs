using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Harmonee.SharedKernel.AspNetCore;

public static class ModuleServices
{
    /// <summary>
    /// Registers services every module may depend on. Idempotent, so each module calls it and works in any host
    /// (the API, the migration service, tests) without relying on the host to remember.
    /// </summary>
    public static IServiceCollection AddHarmoneeModuleServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentUser, HttpCurrentUser>();
        services.TryAddSingleton<AuditLog>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
