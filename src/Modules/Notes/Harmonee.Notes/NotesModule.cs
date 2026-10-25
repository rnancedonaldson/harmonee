using Harmonee.Notes.Features;
using Harmonee.Notes.Infrastructure;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

namespace Harmonee.Notes;

public static class NotesModule
{
    public static IHostApplicationBuilder AddNotesModule(this IHostApplicationBuilder builder) =>
        builder.AddModuleDbContext<NotesDbContext>(NotesDbContext.Schema);

    public static IEndpointRouteBuilder MapNotesEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapNoteEndpoints();
        return routes;
    }
}
