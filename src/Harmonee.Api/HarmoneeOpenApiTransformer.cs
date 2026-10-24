using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Harmonee.Api;

/// <summary>Sets document metadata and describes the API's bearer-token authentication.</summary>
internal sealed class HarmoneeOpenApiTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "Harmonee API";
        document.Info.Description =
            "Family organisation API: families, members and roles, calendar, shopping lists, meal plans and notes. " +
            "Family-scoped endpoints return 404 to non-members and 403 to members whose role lacks the permission.";

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token from Microsoft Entra External ID with the Harmonee.Access scope.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
        });

        return Task.CompletedTask;
    }
}
