using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Families.Contracts;
using Harmonee.Families.Domain;
using Harmonee.Families.Infrastructure;
using Harmonee.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.Families.Features;

/// <summary>Shared reads and aggregate loading for family endpoints.</summary>
internal static class FamilyQueries
{
    public static readonly Error ConcurrentChange = Error.Conflict(
        "family.concurrent_change", "Someone else changed this family at the same time. Please try again.");

    public static Task<Family> LoadAggregateAsync(this FamiliesDbContext db, Guid familyId, CancellationToken cancellationToken) =>
        db.Families.AsTracking()
            .Include(f => f.Members)
            .Include(f => f.Roles)
            .AsSplitQuery()
            .SingleAsync(f => f.Id == familyId, cancellationToken);

    public static async Task<Result> SaveFamilyAsync(this FamiliesDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrentChange;
        }
    }

    public static async Task<FamilyResponse> GetFamilyResponseAsync(
        this FamiliesDbContext db, FamilyAccess access, CancellationToken cancellationToken)
    {
        var familyId = access.FamilyId;
        var family = await db.Families
            .Where(f => f.Id == familyId)
            .Select(f => new { f.Id, f.Name, f.InviteCode })
            .SingleAsync(cancellationToken);

        var roles = await db.Roles
            .Where(r => r.FamilyId == familyId)
            .OrderBy(r => r.Name)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Permissions,
                r.IsDefault,
                MemberCount = db.Members.Count(m => m.RoleId == r.Id),
            })
            .ToListAsync(cancellationToken);

        var members = await (
                from m in db.Members
                join u in db.Users on m.UserId equals u.Id
                join r in db.Roles on m.RoleId equals r.Id
                where m.FamilyId == familyId
                orderby m.IsOwner descending, u.DisplayName
                select new FamilyMemberResponse(m.Id, u.Id, u.DisplayName, m.IsOwner, r.Id, r.Name, m.JoinedAt))
            .ToListAsync(cancellationToken);

        return new FamilyResponse(
            family.Id,
            family.Name,
            roles.Single(r => r.IsDefault).Id,
            access.Has(FamilyPermission.ManageMembers) ? family.InviteCode : null,
            ToResponse(access),
            members,
            [.. roles.Select(r => new FamilyRoleResponse(r.Id, r.Name, r.Permissions, r.MemberCount))]);
    }

    public static FamilyAccessResponse ToResponse(FamilyAccess access) =>
        new(access.MemberId, access.IsOwner, access.IsAdminOverride, [.. access.Permissions.Order()]);
}
