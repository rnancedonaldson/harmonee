using System;
using Microsoft.Extensions.Hosting;
using Harmonee.Shared.Infrastructure.Extensions;
using Harmonee.FamilyService.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using System.Reflection;
using System.Windows.Input;

namespace Harmonee.FamilyService.Presentation.DependencyInjection;

public static class DependencyInjection
{
    public static IHostApplicationBuilder RegisterFamilyService(this IHostApplicationBuilder builder)
    {
        builder.Services.RegisterHarmoneeContext<FamilyContext>();

        return builder;
    }

    public static IApplicationBuilder MapFamilyService(this Microsoft.AspNetCore.Routing.IRouteBuilder app)
    {
    }
}
