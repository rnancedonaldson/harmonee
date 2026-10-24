using System.Net;
using System.Net.Http.Json;
using Harmonee.Api.Contracts.Calendar;
using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Api.IntegrationTests.Infrastructure;

namespace Harmonee.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class FamilyTests(HarmoneeApiFactory factory)
{
    private static SaveCalendarEventRequest SampleEvent => new(
        "Dentist", null, null, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(1), IsAllDay: false);

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var response = await factory.CreateClient().GetAsync("/v1/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_creates_a_profile_on_first_call()
    {
        var client = factory.CreateClientFor(TestUser.Create("Alice"));

        var me = await (await client.GetAsync("/v1/me")).ReadAsync<MeResponse>();

        me.DisplayName.ShouldBe("Alice");
        me.IsPlatformAdmin.ShouldBeFalse();
        me.Families.ShouldBeEmpty();
    }

    [Fact]
    public async Task Creating_a_family_makes_the_creator_the_owner_with_starter_roles()
    {
        var client = factory.CreateClientFor(TestUser.Create("Alice"));

        var family = await client.CreateFamilyAsync("  The Smiths  ");

        family.Name.ShouldBe("The Smiths");
        family.InviteCode.ShouldNotBeNull().Length.ShouldBe(FieldLimits.InviteCodeLength);
        family.MyAccess.IsOwner.ShouldBeTrue();
        family.MyAccess.Permissions.ShouldBe(FamilyPermissions.All, ignoreOrder: true);
        family.Members.ShouldHaveSingleItem().IsOwner.ShouldBeTrue();
        family.Roles.Select(r => r.Name).ShouldBe(["Member", "Viewer"], ignoreOrder: true);
        family.DefaultRoleId.ShouldBe(family.RoleId("Viewer"));

        var me = await (await client.GetAsync("/v1/me")).ReadAsync<MeResponse>();
        me.Families.ShouldHaveSingleItem().FamilyId.ShouldBe(family.Id);
    }

    [Fact]
    public async Task Invalid_requests_return_validation_problems()
    {
        var client = factory.CreateClientFor(TestUser.Create("Alice"));

        var response = await client.PostAsJsonAsync("/v1/families", new CreateFamilyRequest(""));

        var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
        problem.Extensions.ShouldContainKey("errors");
    }

    [Fact]
    public async Task Joining_with_an_invite_code_grants_the_default_role()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var member = factory.CreateClientFor(TestUser.Create("Member"));
        var (family, _) = await ApiTestExtensions.CreateFamilyWithMemberAsync(owner, member);

        var memberView = await (await member.GetAsync($"/v1/families/{family.Id}")).ReadAsync<FamilyResponse>();

        memberView.MyAccess.IsOwner.ShouldBeFalse();
        memberView.MyAccess.Permissions.ShouldBe(
            [FamilyPermission.ViewCalendar, FamilyPermission.ViewShoppingLists, FamilyPermission.ViewMealPlans, FamilyPermission.ViewNotes],
            ignoreOrder: true);
        memberView.InviteCode.ShouldBeNull("only members who can manage members see the invite code");
    }

    [Fact]
    public async Task Joining_twice_is_a_conflict()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var family = await owner.CreateFamilyAsync();

        var response = await owner.PostAsJsonAsync("/v1/families/join", new JoinFamilyRequest(family.InviteCode!));

        (await response.ReadProblemAsync(HttpStatusCode.Conflict)).Code().ShouldBe("family.already_member");
    }

    [Fact]
    public async Task Unknown_invite_codes_are_not_found()
    {
        var client = factory.CreateClientFor(TestUser.Create("Alice"));

        var response = await client.PostAsJsonAsync("/v1/families/join", new JoinFamilyRequest("ZZZZZZZZ"));

        (await response.ReadProblemAsync(HttpStatusCode.NotFound)).Code().ShouldBe("family.invite_not_found");
    }

    [Fact]
    public async Task Non_members_cannot_see_that_a_family_exists()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var stranger = factory.CreateClientFor(TestUser.Create("Stranger"));
        var family = await owner.CreateFamilyAsync();

        (await stranger.GetAsync($"/v1/families/{family.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.GetAsync($"/v1/families/{family.Id}/events")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.PostAsJsonAsync($"/v1/families/{family.Id}/notes", new { title = "x", content = "y" }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Role_permissions_control_what_members_can_do()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var member = factory.CreateClientFor(TestUser.Create("Member"));
        var (family, memberId) = await ApiTestExtensions.CreateFamilyWithMemberAsync(owner, member);
        var eventsUrl = $"/v1/families/{family.Id}/events";

        // Viewer: can read, cannot write.
        (await member.GetAsync(eventsUrl)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var denied = await member.PostAsJsonAsync(eventsUrl, SampleEvent);
        (await denied.ReadProblemAsync(HttpStatusCode.Forbidden)).Code().ShouldBe("family.permission_required");

        // Promote to Member: can write.
        (await owner.PutAsJsonAsync($"/v1/families/{family.Id}/members/{memberId}", new UpdateMemberRequest(family.RoleId("Member"), IsOwner: false)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await member.PostAsJsonAsync(eventsUrl, SampleEvent)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Custom_roles_can_grant_a_subset_of_permissions()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var kid = factory.CreateClientFor(TestUser.Create("Kid"));
        var (family, kidId) = await ApiTestExtensions.CreateFamilyWithMemberAsync(owner, kid);

        var role = await (await owner.PostAsJsonAsync(
                $"/v1/families/{family.Id}/roles",
                new SaveRoleRequest("Kids", [FamilyPermission.ManageShoppingLists, FamilyPermission.ViewCalendar])))
            .ReadAsync<FamilyRoleResponse>(HttpStatusCode.Created);

        role.Permissions.ShouldBe(
            [FamilyPermission.ViewCalendar, FamilyPermission.ViewShoppingLists, FamilyPermission.ManageShoppingLists],
            ignoreOrder: true,
            "managing implies viewing");

        (await owner.PutAsJsonAsync($"/v1/families/{family.Id}/members/{kidId}", new UpdateMemberRequest(role.Id, IsOwner: false)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await kid.PostAsJsonAsync($"/v1/families/{family.Id}/shopping-lists", new { name = "Snacks" })).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await kid.GetAsync($"/v1/families/{family.Id}/notes")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Roles_in_use_cannot_be_deleted()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var family = await owner.CreateFamilyAsync();

        var response = await owner.DeleteAsync($"/v1/families/{family.Id}/roles/{family.RoleId("Member")}");

        (await response.ReadProblemAsync(HttpStatusCode.Conflict)).Code().ShouldBe("family.role_in_use");
    }

    [Fact]
    public async Task A_family_can_have_several_owners_but_never_zero()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var partner = factory.CreateClientFor(TestUser.Create("Partner"));
        var (family, partnerId) = await ApiTestExtensions.CreateFamilyWithMemberAsync(owner, partner);
        var ownerId = family.Members.Single(m => m.IsOwner).MemberId;
        var memberRole = family.RoleId("Member");

        // The only owner can't step down.
        var stepDown = await owner.PutAsJsonAsync($"/v1/families/{family.Id}/members/{ownerId}", new UpdateMemberRequest(memberRole, IsOwner: false));
        (await stepDown.ReadProblemAsync(HttpStatusCode.Conflict)).Code().ShouldBe("family.last_owner");

        // Make the partner a co-owner; now the original owner can leave.
        var promoted = await (await owner.PutAsJsonAsync($"/v1/families/{family.Id}/members/{partnerId}", new UpdateMemberRequest(memberRole, IsOwner: true)))
            .ReadAsync<FamilyResponse>();
        promoted.Members.Count(m => m.IsOwner).ShouldBe(2);

        (await owner.DeleteAsync($"/v1/families/{family.Id}/members/{ownerId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await owner.GetAsync($"/v1/families/{family.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The remaining owner can't leave.
        var leave = await partner.DeleteAsync($"/v1/families/{family.Id}/members/{partnerId}");
        (await leave.ReadProblemAsync(HttpStatusCode.Conflict)).Code().ShouldBe("family.last_owner");
    }

    [Fact]
    public async Task Only_owners_can_change_ownership()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var manager = factory.CreateClientFor(TestUser.Create("Manager"));
        var (family, managerId) = await ApiTestExtensions.CreateFamilyWithMemberAsync(owner, manager);

        var managers = await (await owner.PostAsJsonAsync($"/v1/families/{family.Id}/roles", new SaveRoleRequest("Managers", [FamilyPermission.ManageMembers])))
            .ReadAsync<FamilyRoleResponse>(HttpStatusCode.Created);
        (await owner.PutAsJsonAsync($"/v1/families/{family.Id}/members/{managerId}", new UpdateMemberRequest(managers.Id, IsOwner: false)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var selfPromotion = await manager.PutAsJsonAsync($"/v1/families/{family.Id}/members/{managerId}", new UpdateMemberRequest(managers.Id, IsOwner: true));

        (await selfPromotion.ReadProblemAsync(HttpStatusCode.Forbidden)).Code().ShouldBe("family.owner_required");
    }

    [Fact]
    public async Task Non_owners_cannot_escalate_their_own_privileges()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var manager = factory.CreateClientFor(TestUser.Create("Manager"));
        var (family, managerId) = await ApiTestExtensions.CreateFamilyWithMemberAsync(owner, manager);
        var familyUrl = $"/v1/families/{family.Id}";

        var managers = await (await owner.PostAsJsonAsync($"{familyUrl}/roles",
                new SaveRoleRequest("Managers", [FamilyPermission.ManageMembers, FamilyPermission.ManageRoles, FamilyPermission.ViewNotes])))
            .ReadAsync<FamilyRoleResponse>(HttpStatusCode.Created);
        (await owner.PutAsJsonAsync($"{familyUrl}/members/{managerId}", new UpdateMemberRequest(managers.Id, IsOwner: false)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Can't move themselves into a more powerful role...
        var selfAssign = await manager.PutAsJsonAsync($"{familyUrl}/members/{managerId}", new UpdateMemberRequest(family.RoleId("Member"), IsOwner: false));
        (await selfAssign.ReadProblemAsync(HttpStatusCode.Forbidden)).Code().ShouldBe("family.own_role");

        // ...or add permissions to the role they hold...
        var editOwnRole = await manager.PutAsJsonAsync($"{familyUrl}/roles/{managers.Id}",
            new SaveRoleRequest("Managers", [.. FamilyPermissions.All]));
        (await editOwnRole.ReadProblemAsync(HttpStatusCode.Forbidden)).Code().ShouldBe("family.own_role");

        // ...or create a role with permissions they don't have.
        var bigRole = await manager.PostAsJsonAsync($"{familyUrl}/roles", new SaveRoleRequest("Everything", [FamilyPermission.ManageCalendar]));
        (await bigRole.ReadProblemAsync(HttpStatusCode.Forbidden)).Code().ShouldBe("family.grant_exceeds_own");

        // Granting a subset of their own permissions is fine.
        (await manager.PostAsJsonAsync($"{familyUrl}/roles", new SaveRoleRequest("Readers", [FamilyPermission.ViewNotes])))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Regenerating_the_invite_code_invalidates_the_old_one()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var joiner = factory.CreateClientFor(TestUser.Create("Joiner"));
        var family = await owner.CreateFamilyAsync();

        var regenerated = await (await owner.PostAsync($"/v1/families/{family.Id}/invite-code", null)).ReadAsync<InviteCodeResponse>();

        regenerated.InviteCode.ShouldNotBe(family.InviteCode);
        (await joiner.PostAsJsonAsync("/v1/families/join", new JoinFamilyRequest(family.InviteCode!))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await joiner.PostAsJsonAsync("/v1/families/join", new JoinFamilyRequest(regenerated.InviteCode.ToLowerInvariant())))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Only_owners_can_rename_the_family()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var member = factory.CreateClientFor(TestUser.Create("Member"));
        var (family, _) = await ApiTestExtensions.CreateFamilyWithMemberAsync(owner, member);
        var request = new UpdateFamilyRequest("Renamed", family.DefaultRoleId);

        (await (await member.PutAsJsonAsync($"/v1/families/{family.Id}", request)).ReadProblemAsync(HttpStatusCode.Forbidden))
            .Code().ShouldBe("family.owner_required");

        var renamed = await (await owner.PutAsJsonAsync($"/v1/families/{family.Id}", request with { DefaultRoleId = family.RoleId("Member") }))
            .ReadAsync<FamilyResponse>();
        renamed.Name.ShouldBe("Renamed");
        renamed.DefaultRoleId.ShouldBe(family.RoleId("Member"));
    }
}
