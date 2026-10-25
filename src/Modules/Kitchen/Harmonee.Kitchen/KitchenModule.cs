using Harmonee.Kitchen.Features;
using Harmonee.Kitchen.Infrastructure;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Harmonee.Kitchen;

public static class KitchenModule
{
    public static IHostApplicationBuilder AddKitchenModule(this IHostApplicationBuilder builder) =>
        builder.AddModuleDbContext<KitchenDbContext>(KitchenDbContext.Schema);

    public static IEndpointRouteBuilder MapKitchenEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapShoppingListEndpoints();
        routes.MapMealPlanEndpoints();
        return routes;
    }
}
