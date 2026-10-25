using Harmonee.Api.Contracts.Common;
using Harmonee.Families.Domain;

namespace Harmonee.Families.UnitTests;

public sealed class FamilyTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid s_creator = Guid.CreateVersion7();

    private static Family NewFamily() => Family.Create("The Smiths", s_creator, "ABCDEFGH", s_now).Value;

    [Fact]
    public void Create_makes_the_creator_the_only_owner_with_starter_roles()
    {
        var family = NewFamily();

        var owner = family.Members.ShouldHaveSingleItem();
        owner.UserId.ShouldBe(s_creator);
        owner.IsOwner.ShouldBeTrue();
        family.Roles.Select(r => r.Name).ShouldBe(["Member", "Viewer"]);
        family.DefaultRole.Name.ShouldBe("Viewer");
        family.DefaultRole.Permissions.ShouldBe(FamilyRole.ViewEverything, ignoreOrder: true);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_requires_a_name(string name)
    {
        var result = Family.Create(name, s_creator, "ABCDEFGH", s_now);

        result.Error.ShouldBe(FamilyErrors.NameRequired);
    }

    [Fact]
    public void Create_rejects_names_that_are_too_long()
    {
        var result = Family.Create(new string('x', FieldLimits.NameMaxLength + 1), s_creator, "ABCDEFGH", s_now);

        result.Error.ShouldBe(FamilyErrors.NameTooLong);
    }

    [Fact]
    public void New_members_get_the_default_role_and_cannot_join_twice()
    {
        var family = NewFamily();
        var userId = Guid.CreateVersion7();

        var member = family.AddMember(userId, s_now).Value;

        member.RoleId.ShouldBe(family.DefaultRole.Id);
        member.IsOwner.ShouldBeFalse();
        family.AddMember(userId, s_now).Error.ShouldBe(FamilyErrors.AlreadyMember);
    }

    [Fact]
    public void The_last_owner_cannot_be_demoted_or_removed()
    {
        var family = NewFamily();
        var owner = family.Members.Single();

        family.UpdateMember(owner.Id, family.DefaultRole.Id, isOwner: false, s_now).Error.ShouldBe(FamilyErrors.LastOwner);
        family.RemoveMember(owner.Id, s_now).Error.ShouldBe(FamilyErrors.LastOwner);
    }

    [Fact]
    public void An_owner_can_leave_once_there_is_another_owner()
    {
        var family = NewFamily();
        var original = family.Members.Single();
        var partner = family.AddMember(Guid.CreateVersion7(), s_now).Value;

        family.UpdateMember(partner.Id, partner.RoleId, isOwner: true, s_now).IsSuccess.ShouldBeTrue();
        family.RemoveMember(original.Id, s_now).IsSuccess.ShouldBeTrue();

        family.Members.ShouldHaveSingleItem().Id.ShouldBe(partner.Id);
    }

    [Fact]
    public void Members_can_only_be_assigned_roles_from_their_family()
    {
        var family = NewFamily();
        var member = family.AddMember(Guid.CreateVersion7(), s_now).Value;

        family.UpdateMember(member.Id, Guid.CreateVersion7(), isOwner: false, s_now).Error.ShouldBe(FamilyErrors.RoleNotFound);
    }

    [Fact]
    public void Role_names_are_unique_ignoring_case()
    {
        var family = NewFamily();

        family.AddRole("member", [], s_now).Error.ShouldBe(FamilyErrors.RoleNameTaken);
        family.AddRole("Kids", [], s_now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Roles_in_use_or_default_cannot_be_deleted()
    {
        var family = NewFamily();
        var memberRole = family.Roles.Single(r => string.Equals(r.Name, "Member", StringComparison.Ordinal));

        family.RemoveRole(memberRole.Id, s_now).Error.ShouldBe(FamilyErrors.RoleInUse, "the owner holds the Member role");
        family.RemoveRole(family.DefaultRole.Id, s_now).Error.ShouldBe(FamilyErrors.DefaultRoleCannotBeDeleted);

        var unused = family.AddRole("Guests", [FamilyPermission.ViewCalendar], s_now).Value;
        family.RemoveRole(unused.Id, s_now).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Exactly_one_role_is_the_default()
    {
        var family = NewFamily();
        var memberRole = family.Roles.Single(r => string.Equals(r.Name, "Member", StringComparison.Ordinal));

        family.SetDefaultRole(memberRole.Id, s_now).IsSuccess.ShouldBeTrue();

        family.Roles.Count(r => r.IsDefault).ShouldBe(1);
        family.DefaultRole.Id.ShouldBe(memberRole.Id);
    }

    [Fact]
    public void Every_change_touches_the_aggregate_for_optimistic_concurrency()
    {
        var family = NewFamily();
        var later = s_now.AddMinutes(5);

        family.AddRole("Kids", [], later);

        family.UpdatedAt.ShouldBe(later);
    }
}
