using Harmonee.Api.Contracts.Common;
using Harmonee.Families.Domain;

namespace Harmonee.Families.UnitTests;

public sealed class FamilyRoleTests
{
    [Fact]
    public void Managing_implies_viewing()
    {
        var permissions = FamilyRole.Normalize([FamilyPermission.ManageNotes, FamilyPermission.ManageCalendar]);

        permissions.ShouldBe(
            [FamilyPermission.ViewCalendar, FamilyPermission.ManageCalendar, FamilyPermission.ViewNotes, FamilyPermission.ManageNotes],
            ignoreOrder: true);
    }

    [Fact]
    public void Duplicates_and_undefined_values_are_dropped()
    {
        var permissions = FamilyRole.Normalize([FamilyPermission.ViewNotes, FamilyPermission.ViewNotes, (FamilyPermission)999]);

        permissions.ShouldBe([FamilyPermission.ViewNotes]);
    }

    [Fact]
    public void Starter_roles_cover_content_but_not_administration()
    {
        FamilyRole.EditEverything.ShouldNotContain(FamilyPermission.ManageMembers);
        FamilyRole.EditEverything.ShouldNotContain(FamilyPermission.ManageRoles);
        FamilyRole.ViewEverything.ShouldAllBe(p => p.ToString().StartsWith("View", StringComparison.Ordinal));
    }
}

public sealed class InviteCodeTests
{
    [Fact]
    public void Codes_are_eight_unambiguous_characters()
    {
        var codes = Enumerable.Range(0, 200).Select(_ => InviteCodes.Generate()).ToList();

        codes.ShouldAllBe(c => c.Length == FieldLimits.InviteCodeLength);
        string.Concat(codes).ShouldNotContain('0');
        string.Concat(codes).ShouldNotContain('O');
        string.Concat(codes).ShouldNotContain('1');
        string.Concat(codes).ShouldNotContain('I');
        codes.Distinct(StringComparer.Ordinal).Count().ShouldBeGreaterThan(195);
    }

    [Fact]
    public void Codes_are_normalized_for_lookup() =>
        InviteCodes.Normalize("  abcd2345 ").ShouldBe("ABCD2345");
}

public sealed class UserProfileTests
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("Alice Smith", "alice@example.com", "Alice Smith")]
    [InlineData(null, "alice@example.com", "alice")]
    [InlineData("  ", null, "Family member")]
    public void Display_names_fall_back_sensibly(string? name, string? email, string expected) =>
        new UserProfile("oid", "tid", name, email, s_now).DisplayName.ShouldBe(expected);

    [Fact]
    public void Refresh_only_reports_changes_worth_saving()
    {
        var profile = new UserProfile("oid", "tid", "Alice", "alice@example.com", s_now);

        profile.Refresh("Alice", "alice@example.com", s_now.AddMinutes(5)).ShouldBeFalse();
        profile.Refresh("Alice B", "alice@example.com", s_now.AddMinutes(6)).ShouldBeTrue();
        profile.DisplayName.ShouldBe("Alice B");
        profile.Refresh("Alice B", "alice@example.com", s_now.AddHours(3)).ShouldBeTrue("last-seen is refreshed hourly");
    }
}
