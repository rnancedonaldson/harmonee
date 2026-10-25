using Harmonee.Api.Contracts.Common;

namespace Harmonee.Families.Contracts;

/// <summary>The current user's effective access to one family.</summary>
/// <param name="FamilyId">The family.</param>
/// <param name="UserId">The current user's internal ID.</param>
/// <param name="MemberId">The user's membership, or null for a platform administrator who isn't a member.</param>
/// <param name="IsOwner">Whether the user owns the family.</param>
/// <param name="IsAdminOverride">Whether access is granted only because the user is a platform administrator.</param>
/// <param name="Permissions">Effective permissions (owners and administrators have all of them).</param>
public sealed record FamilyAccess(
    Guid FamilyId,
    Guid UserId,
    Guid? MemberId,
    bool IsOwner,
    bool IsAdminOverride,
    IReadOnlySet<FamilyPermission> Permissions)
{
    private static readonly IReadOnlySet<FamilyPermission> s_all = FamilyPermissions.All.ToHashSet();

    /// <summary>Owners and platform administrators can perform owner-only actions (ownership changes, renaming).</summary>
    public bool CanActAsOwner => IsOwner || IsAdminOverride;

    public bool Has(FamilyPermission permission) => Permissions.Contains(permission);

    public static FamilyAccess ForMember(Guid familyId, Guid userId, Guid memberId, bool isOwner, IEnumerable<FamilyPermission> rolePermissions) =>
        new(familyId, userId, memberId, isOwner, IsAdminOverride: false, isOwner ? s_all : rolePermissions.ToHashSet());

    public static FamilyAccess ForPlatformAdmin(Guid familyId, Guid userId) =>
        new(familyId, userId, MemberId: null, IsOwner: false, IsAdminOverride: true, s_all);
}

/// <summary>Resolves the current user's access to families. Implemented by the Families module.</summary>
public interface IFamilyAccessService
{
    /// <summary>Returns the current user's access to the family, or null if the family doesn't exist or the user has no access.</summary>
    Task<FamilyAccess?> GetAccessAsync(Guid familyId, CancellationToken cancellationToken);

    /// <summary>Returns the current user's internal ID, creating their profile on first use.</summary>
    Task<Guid> GetCurrentUserIdAsync(CancellationToken cancellationToken);
}

/// <summary>Per-request holder for the access resolved by <see cref="FamilyAuthorizationExtensions.RequireFamilyPermission{TBuilder}"/>.</summary>
public sealed class CurrentFamilyAccess
{
    private FamilyAccess? _value;

    public FamilyAccess Value =>
        _value ?? throw new InvalidOperationException("Family access has not been resolved. Is the endpoint missing RequireFamilyPermission()?");

    internal void Set(FamilyAccess access) => _value = access;
}
