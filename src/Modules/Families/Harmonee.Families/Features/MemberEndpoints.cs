using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Families.Contracts;
using Harmonee.Families.Domain;
using Harmonee.Families.Infrastructure;
using Harmonee.SharedKernel;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Harmonee.Families.Features;

internal static class MemberEndpoints
{
    public static void MapMemberEndpoints(this IEndpointRouteBuilder routes)
    {
        var members = routes.MapGroup("/families/{familyId:guid}/members").WithTags("Members");

        members.MapPut("/{memberId:guid}", UpdateAsync)
            .WithName("UpdateMember")
            .WithSummary("Changes a member's role or ownership")
            .WithDescription("Requires ManageMembers. Granting or removing ownership, or changing an owner, requires being an owner.")
            .RequireFamilyPermission(FamilyPermission.ManageMembers);

        members.MapDelete("/{memberId:guid}", RemoveAsync)
            .WithName("RemoveMember")
            .WithSummary("Removes a member from the family, or leaves the family when removing yourself")
            .RequireFamilyPermission();
    }

    private static async Task<Results<Ok<FamilyResponse>, ProblemHttpResult>> UpdateAsync(
        Guid memberId,
        UpdateMemberRequest request,
        CurrentFamilyAccess currentAccess,
        FamiliesDbContext db,
        TimeProvider clock,
        AuditLog audit,
        CancellationToken cancellationToken)
    {
        var access = currentAccess.Value;
        var family = await db.LoadAggregateAsync(access.FamilyId, cancellationToken);
        var target = family.Members.FirstOrDefault(m => m.Id == memberId);
        if (target is null)
        {
            return FamilyErrors.MemberNotFound.ToProblem();
        }

        var touchesOwnership = target.IsOwner || request.IsOwner;
        if (touchesOwnership && !access.CanActAsOwner)
        {
            return FamilyErrors.OwnerRequired.ToProblem();
        }

        // Prevent privilege escalation by non-owners: no self-promotion, and no assigning roles more powerful than your own.
        if (!access.CanActAsOwner)
        {
            if (target.Id == access.MemberId)
            {
                return FamilyErrors.CannotChangeOwnRole.ToProblem();
            }

            var role = family.Roles.FirstOrDefault(r => r.Id == request.RoleId);
            if (role is not null && !role.Permissions.TrueForAll(access.Has))
            {
                return FamilyErrors.CannotGrantMoreThanYouHave.ToProblem();
            }
        }

        var result = family.UpdateMember(memberId, request.RoleId, request.IsOwner, clock.GetUtcNow());
        if (result.IsSuccess)
        {
            result = await db.SaveFamilyAsync(cancellationToken);
        }

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        audit.FamilyChanged(access.UserId, family.Id, "member.updated", memberId.ToString());
        return TypedResults.Ok(await db.GetFamilyResponseAsync(await RefreshAccessAsync(access, db, cancellationToken), cancellationToken));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveAsync(
        Guid memberId,
        CurrentFamilyAccess currentAccess,
        FamiliesDbContext db,
        TimeProvider clock,
        AuditLog audit,
        CancellationToken cancellationToken)
    {
        var access = currentAccess.Value;
        var family = await db.LoadAggregateAsync(access.FamilyId, cancellationToken);
        var target = family.Members.FirstOrDefault(m => m.Id == memberId);
        if (target is null)
        {
            return FamilyErrors.MemberNotFound.ToProblem();
        }

        var isSelf = target.Id == access.MemberId;
        if (!isSelf)
        {
            if (!access.Has(FamilyPermission.ManageMembers))
            {
                return Error.Forbidden("family.permission_required", "Your role in this family doesn't allow this (ManageMembers).").ToProblem();
            }

            if (target.IsOwner && !access.CanActAsOwner)
            {
                return FamilyErrors.OwnerRequired.ToProblem();
            }
        }

        var result = family.RemoveMember(memberId, clock.GetUtcNow());
        if (result.IsSuccess)
        {
            result = await db.SaveFamilyAsync(cancellationToken);
        }

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        audit.FamilyChanged(access.UserId, family.Id, isSelf ? "member.left" : "member.removed", memberId.ToString());
        return TypedResults.NoContent();
    }

    /// <summary>The caller may have just changed their own role or ownership; re-read their access for the response.</summary>
    private static async Task<FamilyAccess> RefreshAccessAsync(FamilyAccess access, FamiliesDbContext db, CancellationToken cancellationToken)
    {
        if (access.IsAdminOverride)
        {
            return access;
        }

        var member = await db.LoadAggregateAsync(access.FamilyId, cancellationToken);
        var me = member.Members.Single(m => m.Id == access.MemberId);
        var role = member.Roles.Single(r => r.Id == me.RoleId);
        return FamilyAccess.ForMember(access.FamilyId, access.UserId, me.Id, me.IsOwner, role.Permissions);
    }
}
