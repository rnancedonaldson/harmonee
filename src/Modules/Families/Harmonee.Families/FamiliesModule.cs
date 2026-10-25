using Harmonee.Families.Contracts;
using Harmonee.Families.Features;
using Harmonee.Families.Infrastructure;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Harmonee.Families;

public static class FamiliesModule
{
    public static IHostApplicationBuilder AddFamiliesModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddModuleDbContext<FamiliesDbContext>(FamiliesDbContext.Schema);
        builder.Services.AddScoped<FamilyAccessService>();
        builder.Services.AddScoped<IFamilyAccessService>(sp => sp.GetRequiredService<FamilyAccessService>());
        builder.Services.AddFamilyAuthorization();
        return builder;
    }

    public static IEndpointRouteBuilder MapFamiliesEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapMeEndpoints();
        routes.MapFamilyEndpoints();
        routes.MapMemberEndpoints();
        routes.MapRoleEndpoints();
        routes.MapAdminEndpoints();
        return routes;
    }
}
