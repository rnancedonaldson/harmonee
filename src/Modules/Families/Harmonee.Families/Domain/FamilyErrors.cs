using Harmonee.Api.Contracts.Common;
using Harmonee.SharedKernel;

namespace Harmonee.Families.Domain;

internal static class FamilyErrors
{
    public static readonly Error NameRequired = Error.Validation("family.name_required", "A name is required.", "name");
    public static readonly Error NameTooLong = Error.Validation("family.name_too_long", $"Names can be at most {FieldLimits.NameMaxLength} characters.", "name");
    public static readonly Error AlreadyMember = Error.Conflict("family.already_member", "You're already a member of this family.");
    public static readonly Error MemberLimitReached = Error.Conflict("family.member_limit", $"A family can have at most {Family.MaxMembers} members.");
    public static readonly Error MemberNotFound = Error.NotFound("family.member_not_found", "The member was not found.");
    public static readonly Error LastOwner = Error.Conflict("family.last_owner", "A family must always have at least one owner.");
    public static readonly Error RoleNotFound = Error.NotFound("family.role_not_found", "The role was not found.");
    public static readonly Error RoleNameTaken = Error.Conflict("family.role_name_taken", "Another role already has that name.");
    public static readonly Error RoleLimitReached = Error.Conflict("family.role_limit", $"A family can have at most {Family.MaxRoles} roles.");
    public static readonly Error RoleInUse = Error.Conflict("family.role_in_use", "Move members to another role before deleting this one.");
    public static readonly Error DefaultRoleCannotBeDeleted = Error.Conflict("family.default_role", "Choose a different default role before deleting this one.");
    public static readonly Error InviteNotFound = Error.NotFound("family.invite_not_found", "That invite code isn't valid.");
    public static readonly Error OwnerRequired = Error.Forbidden("family.owner_required", "Only a family owner can do this.");
    public static readonly Error CannotChangeOwnRole = Error.Forbidden("family.own_role", "You can't change your own role or its permissions. Ask a family owner.");
    public static readonly Error CannotGrantMoreThanYouHave = Error.Forbidden("family.grant_exceeds_own", "You can only grant permissions that you have yourself.");
}
