using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Families.Contracts;
using Harmonee.Families.Infrastructure;
using Harmonee.SharedKernel;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Harmonee.Families.Features;

internal static class RoleEndpoints
{
    public static void MapRoleEndpoints(this IEndpointRouteBuilder routes)
    {
        var roles = routes.MapGroup("/families/{familyId:guid}/roles")
            .WithTags("Roles")
            .RequireFamilyPermission(FamilyPermission.ManageRoles);

        roles.MapPost("/", CreateAsync)
            .WithName("CreateRole")
            .WithSummary("Creates a custom role");

        roles.MapPut("/{roleId:guid}", UpdateAsync)
            .WithName("UpdateRole")
            .WithSummary("Renames a role and replaces its permissions");

        roles.MapDelete("/{roleId:guid}", DeleteAsync)
            .WithName("DeleteRole")
            .WithSummary("Deletes a role that has no members and isn't the default");
    }

    private static async Task<Results<Created<FamilyRoleResponse>, ProblemHttpResult>> CreateAsync(
        SaveRoleRequest request,
        CurrentFamilyAccess currentAccess,
        FamiliesDbContext db,
        TimeProvider clock,
        AuditLog audit,
        CancellationToken cancellationToken)
    {
        var access = currentAccess.Value;
        var family = await db.LoadAggregateAsync(access.FamilyId, cancellationToken);
        if (CheckEscalation(access, family, roleId: null, request.Permissions) is { } denied)
        {
            return denied.ToProblem();
        }

        var added = family.AddRole(request.Name, request.Permissions, clock.GetUtcNow());
        if (added.IsFailure)
        {
            return added.ToProblem();
        }

        var saved = await db.SaveFamilyAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.ToProblem();
        }

        var role = added.Value;
        audit.FamilyChanged(access.UserId, family.Id, "role.created", role.Id.ToString());
        return TypedResults.Created(
            $"/v1/families/{family.Id}/roles/{role.Id}",
            new FamilyRoleResponse(role.Id, role.Name, role.Permissions, MemberCount: 0));
    }

    private static Task<Results<NoContent, ProblemHttpResult>> UpdateAsync(
        Guid roleId,
        SaveRoleRequest request,
        CurrentFamilyAccess currentAccess,
        FamiliesDbContext db,
        TimeProvider clock,
        AuditLog audit,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            currentAccess.Value, db, audit, "role.updated", roleId,
            family => CheckEscalation(currentAccess.Value, family, roleId, request.Permissions) is { } denied
                ? denied
                : family.UpdateRole(roleId, request.Name, request.Permissions, clock.GetUtcNow()),
            cancellationToken);

    private static Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid roleId,
        CurrentFamilyAccess currentAccess,
        FamiliesDbContext db,
        TimeProvider clock,
        AuditLog audit,
        CancellationToken cancellationToken) =>
        ChangeAsync(
            currentAccess.Value, db, audit, "role.deleted", roleId,
            family => family.RemoveRole(roleId, clock.GetUtcNow()),
            cancellationToken);

    /// <summary>
    /// Non-owners with ManageRoles can't escalate: they can't edit the role they hold, and can only grant permissions they have.
    /// </summary>
    private static Error? CheckEscalation(FamilyAccess access, Domain.Family family, Guid? roleId, IEnumerable<FamilyPermission> requested)
    {
        if (access.CanActAsOwner)
        {
            return null;
        }

        var ownRoleId = family.Members.FirstOrDefault(m => m.Id == access.MemberId)?.RoleId;
        if (roleId is not null && roleId == ownRoleId)
        {
            return Domain.FamilyErrors.CannotChangeOwnRole;
        }

        return Domain.FamilyRole.Normalize(requested).TrueForAll(access.Has) ? null : Domain.FamilyErrors.CannotGrantMoreThanYouHave;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ChangeAsync(
        FamilyAccess access,
        FamiliesDbContext db,
        AuditLog audit,
        string action,
        Guid roleId,
        Func<Domain.Family, Result> change,
        CancellationToken cancellationToken)
    {
        var family = await db.LoadAggregateAsync(access.FamilyId, cancellationToken);
        var result = change(family);
        if (result.IsSuccess)
        {
            result = await db.SaveFamilyAsync(cancellationToken);
        }

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        audit.FamilyChanged(access.UserId, family.Id, action, roleId.ToString());
        return TypedResults.NoContent();
    }
}
