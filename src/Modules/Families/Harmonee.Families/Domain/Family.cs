using Harmonee.Api.Contracts.Common;
using Harmonee.SharedKernel;

namespace Harmonee.Families.Domain;

/// <summary>
/// A family: the context in which all shared data (calendar, lists, meals, notes) lives.
/// Aggregate root for its members and roles; enforces that there is always at least one owner.
/// </summary>
internal sealed class Family
{
    public const int MaxMembers = 50;
    public const int MaxRoles = 20;

    private readonly List<FamilyMember> _members = [];
    private readonly List<FamilyRole> _roles = [];

    private Family()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string InviteCode { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Updated by every change to the aggregate so concurrent changes conflict on the row version.</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<FamilyMember> Members => _members;

    public IReadOnlyList<FamilyRole> Roles => _roles;

    public FamilyRole DefaultRole => _roles.Single(r => r.IsDefault);

    /// <summary>Creates a family owned by its creator, with two starter roles: "Member" (edit everything) and "Viewer" (read-only, default for people who join).</summary>
    public static Result<Family> Create(string name, Guid creatorUserId, string inviteCode, DateTimeOffset now)
    {
        var validName = ValidateName(name);
        if (validName.IsFailure)
        {
            return validName.Error!;
        }

        var family = new Family
        {
            Id = Guid.CreateVersion7(),
            Name = validName.Value,
            InviteCode = inviteCode,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var member = new FamilyRole(family.Id, "Member", FamilyRole.EditEverything, isDefault: false);
        var viewer = new FamilyRole(family.Id, "Viewer", FamilyRole.ViewEverything, isDefault: true);
        family._roles.AddRange([member, viewer]);
        family._members.Add(new FamilyMember(family.Id, creatorUserId, member.Id, isOwner: true, now));
        return family;
    }

    public Result Rename(string name, DateTimeOffset now)
    {
        var validName = ValidateName(name);
        if (validName.IsFailure)
        {
            return validName.Error!;
        }

        Name = validName.Value;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result<FamilyMember> AddMember(Guid userId, DateTimeOffset now)
    {
        if (_members.Exists(m => m.UserId == userId))
        {
            return FamilyErrors.AlreadyMember;
        }

        if (_members.Count >= MaxMembers)
        {
            return FamilyErrors.MemberLimitReached;
        }

        var member = new FamilyMember(Id, userId, DefaultRole.Id, isOwner: false, now);
        _members.Add(member);
        UpdatedAt = now;
        return member;
    }

    public Result UpdateMember(Guid memberId, Guid roleId, bool isOwner, DateTimeOffset now)
    {
        var member = _members.Find(m => m.Id == memberId);
        if (member is null)
        {
            return FamilyErrors.MemberNotFound;
        }

        if (!_roles.Exists(r => r.Id == roleId))
        {
            return FamilyErrors.RoleNotFound;
        }

        if (member.IsOwner && !isOwner && OwnerCount == 1)
        {
            return FamilyErrors.LastOwner;
        }

        member.Update(roleId, isOwner);
        UpdatedAt = now;
        return Result.Success();
    }

    public Result RemoveMember(Guid memberId, DateTimeOffset now)
    {
        var member = _members.Find(m => m.Id == memberId);
        if (member is null)
        {
            return FamilyErrors.MemberNotFound;
        }

        if (member.IsOwner && OwnerCount == 1)
        {
            return FamilyErrors.LastOwner;
        }

        _members.Remove(member);
        UpdatedAt = now;
        return Result.Success();
    }

    public Result<FamilyRole> AddRole(string name, IEnumerable<FamilyPermission> permissions, DateTimeOffset now)
    {
        if (_roles.Count >= MaxRoles)
        {
            return FamilyErrors.RoleLimitReached;
        }

        var validName = ValidateRoleName(name, existingRoleId: null);
        if (validName.IsFailure)
        {
            return validName.Error!;
        }

        var role = new FamilyRole(Id, validName.Value, permissions, isDefault: false);
        _roles.Add(role);
        UpdatedAt = now;
        return role;
    }

    public Result UpdateRole(Guid roleId, string name, IEnumerable<FamilyPermission> permissions, DateTimeOffset now)
    {
        var role = _roles.Find(r => r.Id == roleId);
        if (role is null)
        {
            return FamilyErrors.RoleNotFound;
        }

        var validName = ValidateRoleName(name, roleId);
        if (validName.IsFailure)
        {
            return validName.Error!;
        }

        role.Update(validName.Value, permissions);
        UpdatedAt = now;
        return Result.Success();
    }

    public Result RemoveRole(Guid roleId, DateTimeOffset now)
    {
        var role = _roles.Find(r => r.Id == roleId);
        if (role is null)
        {
            return FamilyErrors.RoleNotFound;
        }

        if (role.IsDefault)
        {
            return FamilyErrors.DefaultRoleCannotBeDeleted;
        }

        if (_members.Exists(m => m.RoleId == roleId))
        {
            return FamilyErrors.RoleInUse;
        }

        _roles.Remove(role);
        UpdatedAt = now;
        return Result.Success();
    }

    public Result SetDefaultRole(Guid roleId, DateTimeOffset now)
    {
        var role = _roles.Find(r => r.Id == roleId);
        if (role is null)
        {
            return FamilyErrors.RoleNotFound;
        }

        foreach (var r in _roles)
        {
            r.SetDefault(r.Id == roleId);
        }

        UpdatedAt = now;
        return Result.Success();
    }

    public void RegenerateInviteCode(string inviteCode, DateTimeOffset now)
    {
        InviteCode = inviteCode;
        UpdatedAt = now;
    }

    private int OwnerCount => _members.Count(m => m.IsOwner);

    private static Result<string> ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return FamilyErrors.NameRequired;
        }

        return trimmed.Length > FieldLimits.NameMaxLength ? FamilyErrors.NameTooLong : trimmed;
    }

    private Result<string> ValidateRoleName(string? name, Guid? existingRoleId)
    {
        var validName = ValidateName(name);
        if (validName.IsFailure)
        {
            return validName;
        }

        var taken = _roles.Exists(r => r.Id != existingRoleId && string.Equals(r.Name, validName.Value, StringComparison.OrdinalIgnoreCase));
        return taken ? FamilyErrors.RoleNameTaken : validName;
    }
}
