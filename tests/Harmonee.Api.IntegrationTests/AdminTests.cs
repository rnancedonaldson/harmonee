using System.Net;
using System.Net.Http.Json;
using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Api.Contracts.Notes;
using Harmonee.Api.IntegrationTests.Infrastructure;

namespace Harmonee.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class AdminTests(HarmoneeApiFactory factory)
{
    [Fact]
    public async Task Non_admins_cannot_use_admin_endpoints()
    {
        var client = factory.CreateClientFor(TestUser.Create("Alice"));

        (await client.GetAsync("/v1/admin/families")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admins_can_find_families_and_manage_their_data()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var admin = factory.CreateClientFor(TestUser.Create("Admin", isPlatformAdmin: true));
        var uniqueName = $"Admin target {Guid.NewGuid():N}";
        var family = await owner.CreateFamilyAsync(uniqueName);
        await owner.PostAsJsonAsync($"/v1/families/{family.Id}/notes", new SaveNoteRequest("Wifi password", "Ask Mum"));

        var search = await (await admin.GetAsync($"/v1/admin/families?search={Uri.EscapeDataString(uniqueName)}"))
            .ReadAsync<CursorPage<AdminFamilySummary>>();
        var found = search.Items.ShouldHaveSingleItem();
        found.Id.ShouldBe(family.Id);
        found.MemberCount.ShouldBe(1);
        found.OwnerNames.ShouldBe(["Owner"]);

        // Admins aren't members, but get full (audited) access through the regular endpoints.
        var adminView = await (await admin.GetAsync($"/v1/families/{family.Id}")).ReadAsync<FamilyResponse>();
        adminView.MyAccess.IsAdminOverride.ShouldBeTrue();
        adminView.MyAccess.MemberId.ShouldBeNull();
        adminView.InviteCode.ShouldNotBeNull();

        var notes = await (await admin.GetAsync($"/v1/families/{family.Id}/notes")).ReadAsync<CursorPage<NoteSummary>>();
        var note = notes.Items.ShouldHaveSingleItem();
        (await admin.DeleteAsync($"/v1/families/{family.Id}/notes/{note.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Admins can fix ownership problems, e.g. promote a member.
        var ownerMemberId = adminView.Members.Single().MemberId;
        (await admin.PutAsJsonAsync($"/v1/families/{family.Id}/members/{ownerMemberId}", new UpdateMemberRequest(family.RoleId("Viewer"), IsOwner: true)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_family_list_is_paginated()
    {
        var owner = factory.CreateClientFor(TestUser.Create("Owner"));
        var admin = factory.CreateClientFor(TestUser.Create("Admin", isPlatformAdmin: true));
        var prefix = $"Paged {Guid.NewGuid():N}";
        for (var i = 0; i < 3; i++)
        {
            await owner.CreateFamilyAsync($"{prefix} {i}");
        }

        var first = await (await admin.GetAsync($"/v1/admin/families?search={prefix}&limit=2")).ReadAsync<CursorPage<AdminFamilySummary>>();
        first.Items.Count.ShouldBe(2);
        first.NextCursor.ShouldNotBeNull();

        var second = await (await admin.GetAsync($"/v1/admin/families?search={prefix}&limit=2&cursor={first.NextCursor}"))
            .ReadAsync<CursorPage<AdminFamilySummary>>();
        second.Items.ShouldHaveSingleItem();
        second.NextCursor.ShouldBeNull();
        first.Items.Concat(second.Items).Select(f => f.Id).Distinct().Count().ShouldBe(3);
    }
}
