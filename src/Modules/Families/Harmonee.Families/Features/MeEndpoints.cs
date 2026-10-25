using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Families.Infrastructure;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.Families.Features;

internal static class MeEndpoints
{
    public static void MapMeEndpoints(this IEndpointRouteBuilder routes) =>
        routes.MapGet("/me", GetMeAsync)
            .WithTags("Me")
            .WithName("GetMe")
            .WithSummary("Gets the signed-in user's profile and family memberships")
            .WithDescription("Creates the user's profile on first call.");

    private static async Task<Ok<MeResponse>> GetMeAsync(
        FamilyAccessService accessService, FamiliesDbContext db, ICurrentUser currentUser, CancellationToken cancellationToken)
    {
        var profile = await accessService.GetCurrentProfileAsync(cancellationToken);

        var memberships = await (
                from m in db.Members
                join f in db.Families on m.FamilyId equals f.Id
                join r in db.Roles on m.RoleId equals r.Id
                where m.UserId == profile.Id
                orderby f.Name
                select new { FamilyId = f.Id, FamilyName = f.Name, m.IsOwner, RoleName = r.Name, r.Permissions })
            .ToListAsync(cancellationToken);

        var families = memberships
            .Select(m => new FamilyMembershipSummary(
                m.FamilyId,
                m.FamilyName,
                m.IsOwner,
                m.RoleName,
                m.IsOwner ? FamilyPermissions.All : m.Permissions))
            .ToList();

        return TypedResults.Ok(new MeResponse(profile.Id, profile.DisplayName, profile.Email, currentUser.IsPlatformAdmin, families));
    }
}
