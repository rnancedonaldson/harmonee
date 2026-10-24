using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Families.Contracts;
using Harmonee.Families.Domain;
using Harmonee.Families.Infrastructure;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.Families.Features;

internal static class FamilyEndpoints
{
    private const int InviteCodeAttempts = 3;

    public static void MapFamilyEndpoints(this IEndpointRouteBuilder routes)
    {
        var families = routes.MapGroup("/families").WithTags("Families");

        families.MapPost("/", CreateAsync)
            .WithName("CreateFamily")
            .WithSummary("Creates a family owned by the caller");

        families.MapPost("/join", JoinAsync)
            .WithName("JoinFamily")
            .WithSummary("Joins a family using its invite code");

        var family = families.MapGroup("/{familyId:guid}");

        family.MapGet("/", GetAsync)
            .WithName("GetFamily")
            .WithSummary("Gets a family with its members, roles, and the caller's access")
            .RequireFamilyPermission();

        family.MapPut("/", UpdateAsync)
            .WithName("UpdateFamily")
            .WithSummary("Renames the family and sets the default role for people who join (owners only)")
            .RequireFamilyPermission();

        family.MapPost("/invite-code", RegenerateInviteCodeAsync)
            .WithName("RegenerateInviteCode")
            .WithSummary("Replaces the family's invite code; the old code stops working")
            .RequireFamilyPermission(FamilyPermission.ManageMembers);
    }

    private static async Task<Results<Created<FamilyResponse>, ProblemHttpResult>> CreateAsync(
        CreateFamilyRequest request,
        FamilyAccessService accessService,
        FamiliesDbContext db,
        TimeProvider clock,
        AuditLog audit,
        CancellationToken cancellationToken)
    {
        var userId = await accessService.GetCurrentUserIdAsync(cancellationToken);

        for (var attempt = 1; ; attempt++)
        {
            var created = Family.Create(request.Name, userId, InviteCodes.Generate(), clock.GetUtcNow());
            if (created.IsFailure)
            {
                return created.ToProblem();
            }

            db.Families.Add(created.Value);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException) when (attempt < InviteCodeAttempts)
            {
                // Extremely unlikely invite-code collision: discard and retry with a new code.
                db.ChangeTracker.Clear();
                continue;
            }

            var family = created.Value;
            audit.FamilyChanged(userId, family.Id, "family.created");
            var access = FamilyAccess.ForMember(family.Id, userId, family.Members.Single().Id, isOwner: true, []);
            return TypedResults.Created($"/v1/families/{family.Id}", await db.GetFamilyResponseAsync(access, cancellationToken));
        }
    }

    private static async Task<Ok<FamilyResponse>> GetAsync(
        CurrentFamilyAccess access, FamiliesDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(await db.GetFamilyResponseAsync(access.Value, cancellationToken));

    private static async Task<Results<Ok<FamilyResponse>, ProblemHttpResult>> UpdateAsync(
        UpdateFamilyRequest request,
        CurrentFamilyAccess currentAccess,
        FamiliesDbContext db,
        TimeProvider clock,
        AuditLog audit,
        CancellationToken cancellationToken)
    {
        var access = currentAccess.Value;
        if (!access.CanActAsOwner)
        {
            return FamilyErrors.OwnerRequired.ToProblem();
        }

        var family = await db.LoadAggregateAsync(access.FamilyId, cancellationToken);
        var now = clock.GetUtcNow();
        var result = family.Rename(request.Name, now);
        if (result.IsSuccess)
        {
            result = family.SetDefaultRole(request.DefaultRoleId, now);
        }

        if (result.IsSuccess)
        {
            result = await db.SaveFamilyAsync(cancellationToken);
        }

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        audit.FamilyChanged(access.UserId, family.Id, "family.updated");
        return TypedResults.Ok(await db.GetFamilyResponseAsync(access, cancellationToken));
    }

    private static async Task<Results<Created<FamilyMembershipSummary>, ProblemHttpResult>> JoinAsync(
        JoinFamilyRequest request,
        FamilyAccessService accessService,
        FamiliesDbContext db,
        TimeProvider clock,
        AuditLog audit,
        CancellationToken cancellationToken)
    {
        var userId = await accessService.GetCurrentUserIdAsync(cancellationToken);
        var code = InviteCodes.Normalize(request.InviteCode);

        var familyId = await db.Families
            .Where(f => f.InviteCode == code)
            .Select(f => (Guid?)f.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (familyId is null)
        {
            return FamilyErrors.InviteNotFound.ToProblem();
        }

        var family = await db.LoadAggregateAsync(familyId.Value, cancellationToken);
        var added = family.AddMember(userId, clock.GetUtcNow());
        if (added.IsFailure)
        {
            return added.ToProblem();
        }

        var saved = await db.SaveFamilyAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.ToProblem();
        }

        audit.FamilyChanged(userId, family.Id, "member.joined", added.Value.Id.ToString());
        var role = family.Roles.Single(r => r.Id == added.Value.RoleId);
        return TypedResults.Created(
            $"/v1/families/{family.Id}",
            new FamilyMembershipSummary(family.Id, family.Name, IsOwner: false, role.Name, role.Permissions));
    }

    private static async Task<Results<Ok<InviteCodeResponse>, ProblemHttpResult>> RegenerateInviteCodeAsync(
        CurrentFamilyAccess currentAccess,
        FamiliesDbContext db,
        TimeProvider clock,
        AuditLog audit,
        CancellationToken cancellationToken)
    {
        var access = currentAccess.Value;
        var family = await db.LoadAggregateAsync(access.FamilyId, cancellationToken);
        family.RegenerateInviteCode(InviteCodes.Generate(), clock.GetUtcNow());

        var saved = await db.SaveFamilyAsync(cancellationToken);
        if (saved.IsFailure)
        {
            return saved.ToProblem();
        }

        audit.FamilyChanged(access.UserId, family.Id, "invite_code.regenerated");
        return TypedResults.Ok(new InviteCodeResponse(family.InviteCode));
    }
}
