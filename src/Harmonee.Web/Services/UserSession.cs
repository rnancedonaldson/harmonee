using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Web.Api;

namespace Harmonee.Web.Services;

/// <summary>Per-request cache of the signed-in user's profile, so the layout and page share one API call.</summary>
public sealed class UserSession(HarmoneeApiClient api)
{
    private Task<ApiResult<MeResponse>>? _me;

    public Task<ApiResult<MeResponse>> GetMeAsync(CancellationToken cancellationToken) =>
        _me ??= api.GetMeAsync(cancellationToken);
}

public static class PermissionExtensions
{
    public static bool Can(this FamilyAccessResponse access, FamilyPermission permission) => access.Permissions.Contains(permission);

    public static bool Can(this FamilyResponse family, FamilyPermission permission) => family.MyAccess.Can(permission);

    public static bool CanActAsOwner(this FamilyResponse family) => family.MyAccess.IsOwner || family.MyAccess.IsAdminOverride;

    public static string NameOf(this FamilyResponse family, Guid userId) =>
        family.Members.FirstOrDefault(m => m.UserId == userId)?.DisplayName ?? "a former member";
}
