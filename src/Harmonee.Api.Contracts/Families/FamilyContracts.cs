using System.ComponentModel.DataAnnotations;
using Harmonee.Api.Contracts.Common;

namespace Harmonee.Api.Contracts.Families;

public sealed record MeResponse(
    Guid UserId,
    string DisplayName,
    string? Email,
    bool IsPlatformAdmin,
    IReadOnlyList<FamilyMembershipSummary> Families);

public sealed record FamilyMembershipSummary(
    Guid FamilyId,
    string FamilyName,
    bool IsOwner,
    string RoleName,
    IReadOnlyList<FamilyPermission> Permissions);

public sealed record CreateFamilyRequest(
    [Required, StringLength(FieldLimits.NameMaxLength, MinimumLength = 1)] string Name);

public sealed record UpdateFamilyRequest(
    [Required, StringLength(FieldLimits.NameMaxLength, MinimumLength = 1)] string Name,
    Guid DefaultRoleId);

public sealed record JoinFamilyRequest(
    [Required, StringLength(FieldLimits.InviteCodeLength, MinimumLength = FieldLimits.InviteCodeLength)] string InviteCode);

public sealed record FamilyResponse(
    Guid Id,
    string Name,
    Guid DefaultRoleId,
    string? InviteCode,
    FamilyAccessResponse MyAccess,
    IReadOnlyList<FamilyMemberResponse> Members,
    IReadOnlyList<FamilyRoleResponse> Roles);

/// <summary>The caller's effective access to a family.</summary>
/// <param name="MemberId">The caller's membership ID, or null when accessing as a platform administrator who isn't a member.</param>
/// <param name="IsOwner">True when the caller is an owner of the family.</param>
/// <param name="IsAdminOverride">True when access is granted only because the caller is a platform administrator.</param>
/// <param name="Permissions">The caller's effective permissions.</param>
public sealed record FamilyAccessResponse(
    Guid? MemberId,
    bool IsOwner,
    bool IsAdminOverride,
    IReadOnlyList<FamilyPermission> Permissions);

public sealed record FamilyMemberResponse(
    Guid MemberId,
    Guid UserId,
    string DisplayName,
    bool IsOwner,
    Guid RoleId,
    string RoleName,
    DateTimeOffset JoinedAt);

public sealed record FamilyRoleResponse(
    Guid Id,
    string Name,
    IReadOnlyList<FamilyPermission> Permissions,
    int MemberCount);

public sealed record UpdateMemberRequest(Guid RoleId, bool IsOwner);

public sealed record SaveRoleRequest(
    [Required, StringLength(FieldLimits.NameMaxLength, MinimumLength = 1)] string Name,
    [Required] IReadOnlyList<FamilyPermission> Permissions);

public sealed record InviteCodeResponse(string InviteCode);

public sealed record AdminFamilySummary(
    Guid Id,
    string Name,
    int MemberCount,
    IReadOnlyList<string> OwnerNames,
    DateTimeOffset CreatedAt);
