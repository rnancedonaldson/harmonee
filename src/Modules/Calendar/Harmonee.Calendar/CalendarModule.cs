using Harmonee.Calendar.Features;
using Harmonee.Calendar.Infrastructure;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Harmonee.Calendar;

public static class CalendarModule
{
    public static IHostApplicationBuilder AddCalendarModule(this IHostApplicationBuilder builder) =>
        builder.AddModuleDbContext<CalendarDbContext>(CalendarDbContext.Schema);

    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapEventEndpoints();
        return routes;
    }
}
