using Harmonee.Families.Contracts;
using Harmonee.Families.Domain;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.Families.Infrastructure;

/// <summary>Resolves the caller's profile and family access. Scoped: results are cached for the request.</summary>
internal sealed class FamilyAccessService(FamiliesDbContext db, ICurrentUser currentUser, TimeProvider clock) : IFamilyAccessService
{
    private UserProfile? _profile;

    public async Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken) =>
        (await GetCurrentProfileAsync(cancellationToken)).Id;

    public async Task<FamilyAccess?> GetAccessAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync(cancellationToken);

        var membership = await (
                from m in db.Members
                join r in db.Roles on m.RoleId equals r.Id
                where m.FamilyId == familyId && m.UserId == userId
                select new { m.Id, m.IsOwner, r.Permissions })
            .SingleOrDefaultAsync(cancellationToken);

        if (membership is not null)
        {
            return FamilyAccess.ForMember(familyId, userId, membership.Id, membership.IsOwner, membership.Permissions);
        }

        if (currentUser.IsPlatformAdmin && await db.Families.AnyAsync(f => f.Id == familyId, cancellationToken))
        {
            return FamilyAccess.ForPlatformAdmin(familyId, userId);
        }

        return null;
    }

    /// <summary>Gets the caller's profile, creating it on first sign-in and refreshing name/email from the token.</summary>
    public async Task<UserProfile> GetCurrentProfileAsync(CancellationToken cancellationToken)
    {
        if (_profile is not null)
        {
            return _profile;
        }

        var objectId = currentUser.ObjectId;
        var tenantId = currentUser.TenantId;
        var now = clock.GetUtcNow();

        var profile = await db.Users.AsTracking()
            .SingleOrDefaultAsync(u => u.TenantId == tenantId && u.ObjectId == objectId, cancellationToken);

        if (profile is null)
        {
            profile = new UserProfile(objectId, tenantId, currentUser.DisplayName, currentUser.Email, now);
            db.Users.Add(profile);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // A concurrent first request created the profile; use that one.
                db.Entry(profile).State = EntityState.Detached;
                profile = await db.Users.SingleAsync(u => u.TenantId == tenantId && u.ObjectId == objectId, cancellationToken);
            }
        }
        else if (profile.Refresh(currentUser.DisplayName, currentUser.Email, now))
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        db.Entry(profile).State = EntityState.Detached;
        return _profile = profile;
    }
}
