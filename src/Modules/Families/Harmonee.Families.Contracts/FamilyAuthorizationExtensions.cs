using Harmonee.Api.Contracts.Common;
using Harmonee.SharedKernel;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Harmonee.Families.Contracts;

/// <summary>
/// Family-scoped authorization for endpoints under <c>/families/{familyId}</c>.
/// Non-members get 404 (we don't confirm a family exists); members without the permission get 403.
/// </summary>
public static class FamilyAuthorizationExtensions
{
    public const string FamilyIdRouteParameter = "familyId";
    private const string ProblemContentType = "application/problem+json";

    /// <summary>Requires the current user to be a member of the family (or a platform administrator) and, optionally, to hold a permission.</summary>
    public static TBuilder RequireFamilyPermission<TBuilder>(this TBuilder builder, FamilyPermission? permission = null)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(new FamilyPermissionFilter(permission));
        builder.Add(endpoint =>
        {
            endpoint.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status404NotFound, typeof(ProblemDetails), [ProblemContentType]));
            endpoint.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status403Forbidden, typeof(ProblemDetails), [ProblemContentType]));
        });
        return builder;
    }

    /// <summary>Registers the services the family authorization filter depends on.</summary>
    public static IServiceCollection AddFamilyAuthorization(this IServiceCollection services)
    {
        services.AddScoped<CurrentFamilyAccess>();
        return services;
    }

    private sealed class FamilyPermissionFilter(FamilyPermission? permission) : IEndpointFilter
    {
        private static readonly Error s_notFound = Error.NotFound("family.not_found", "The family was not found.");

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var http = context.HttpContext;
            if (!Guid.TryParse(http.GetRouteValue(FamilyIdRouteParameter)?.ToString(), out var familyId))
            {
                return s_notFound.ToProblem();
            }

            var services = http.RequestServices;
            var access = await services.GetRequiredService<IFamilyAccessService>().GetAccessAsync(familyId, http.RequestAborted);
            if (access is null)
            {
                return s_notFound.ToProblem();
            }

            if (permission is { } required && !access.Has(required))
            {
                return Error.Forbidden("family.permission_required", $"Your role in this family doesn't allow this ({required}).").ToProblem();
            }

            if (access.IsAdminOverride)
            {
                services.GetRequiredService<AuditLog>().AdminAccess(
                    services.GetRequiredService<ICurrentUser>().ObjectId, familyId, http.Request.Method, http.Request.Path);
            }

            services.GetRequiredService<CurrentFamilyAccess>().Set(access);
            return await next(context);
        }
    }
}
