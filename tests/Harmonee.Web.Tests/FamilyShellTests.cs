using Bunit;
using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Web.Components.Shared;

namespace Harmonee.Web.Tests;

public sealed class FamilyShellTests : BunitContext
{
    private static FamilyResponse Family(bool isAdminOverride = false, params FamilyPermission[] permissions) =>
        new(
            Guid.NewGuid(),
            "The Smiths",
            Guid.NewGuid(),
            InviteCode: null,
            new FamilyAccessResponse(isAdminOverride ? null : Guid.NewGuid(), IsOwner: false, isAdminOverride, permissions),
            Members: [],
            Roles: []);

    [Fact]
    public void Only_sections_the_role_allows_are_shown()
    {
        var cut = Render<FamilyShell>(p => p
            .Add(x => x.Family, Family(false, FamilyPermission.ViewCalendar, FamilyPermission.ViewNotes))
            .Add(x => x.Title, "Overview"));

        var tabs = cut.FindAll("nav.tabs a").Select(a => a.TextContent).ToList();
        tabs.ShouldBe(["Overview", "Calendar", "Notes", "Members & roles"]);
    }

    [Fact]
    public void Admin_override_shows_the_audit_banner()
    {
        var cut = Render<FamilyShell>(p => p
            .Add(x => x.Family, Family(true, [.. FamilyPermissions.All]))
            .Add(x => x.Title, "Overview"));

        cut.Find("[data-testid=admin-banner]").TextContent.ShouldContain("audit-logged");
    }

    [Fact]
    public void Members_do_not_see_the_admin_banner()
    {
        var cut = Render<FamilyShell>(p => p
            .Add(x => x.Family, Family(false, FamilyPermission.ViewCalendar))
            .Add(x => x.Title, "Overview"));

        cut.FindAll("[data-testid=admin-banner]").ShouldBeEmpty();
    }

    [Fact]
    public void Permission_list_describes_each_permission()
    {
        var cut = Render<PermissionList>(p => p.Add(x => x.Permissions, [FamilyPermission.ManageNotes]));

        cut.Find("li").TextContent.ShouldBe(FamilyPermissions.Describe(FamilyPermission.ManageNotes));
    }
}
