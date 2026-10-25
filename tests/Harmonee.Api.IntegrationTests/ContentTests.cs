using System.Net;
using System.Net.Http.Json;
using Harmonee.Api.Contracts.Calendar;
using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Kitchen;
using Harmonee.Api.Contracts.Notes;
using Harmonee.Api.IntegrationTests.Infrastructure;

namespace Harmonee.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ContentTests(HarmoneeApiFactory factory)
{
    [Fact]
    public async Task Calendar_events_support_crud_with_optimistic_concurrency()
    {
        var client = factory.CreateClientFor(TestUser.Create("Owner"));
        var family = await client.CreateFamilyAsync();
        var url = $"/v1/families/{family.Id}/events";
        var start = new DateTimeOffset(2030, 5, 1, 9, 0, 0, TimeSpan.Zero);

        var created = await (await client.PostAsJsonAsync(url, new SaveCalendarEventRequest("Swim class", "Bring towel", "Leisure centre", start, start.AddHours(1), false)))
            .ReadAsync<CalendarEventResponse>(HttpStatusCode.Created);

        var update = new SaveCalendarEventRequest("Swim lesson", null, null, start, start.AddHours(2), false);

        // No If-Match → 428.
        (await (await client.SendAsync(ApiTestExtensions.Put($"{url}/{created.Id}", update))).ReadProblemAsync(HttpStatusCode.PreconditionRequired))
            .Code().ShouldBe("concurrency.if_match_required");

        // Correct ETag → 200 and a new ETag.
        var updated = await (await client.SendAsync(ApiTestExtensions.Put($"{url}/{created.Id}", update).WithIfMatch(created.ETag)))
            .ReadAsync<CalendarEventResponse>();
        updated.Title.ShouldBe("Swim lesson");
        updated.ETag.ShouldNotBe(created.ETag);

        // Stale ETag → 412.
        (await (await client.SendAsync(ApiTestExtensions.Put($"{url}/{created.Id}", update).WithIfMatch(created.ETag))).ReadProblemAsync(HttpStatusCode.PreconditionFailed))
            .Code().ShouldBe("concurrency.stale");

        var inRange = await (await client.GetAsync($"{url}?from={Uri.EscapeDataString(start.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(start.AddDays(1).ToString("O"))}"))
            .ReadAsync<CalendarEventsResponse>();
        inRange.Items.ShouldHaveSingleItem().Id.ShouldBe(created.Id);

        var outOfRange = await (await client.GetAsync($"{url}?from={Uri.EscapeDataString(start.AddDays(2).ToString("O"))}")).ReadAsync<CalendarEventsResponse>();
        outOfRange.Items.ShouldBeEmpty();

        (await client.DeleteAsync($"{url}/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"{url}/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Events_cannot_end_before_they_start()
    {
        var client = factory.CreateClientFor(TestUser.Create("Owner"));
        var family = await client.CreateFamilyAsync();
        var start = DateTimeOffset.UtcNow;

        var response = await client.PostAsJsonAsync($"/v1/families/{family.Id}/events", new SaveCalendarEventRequest("Oops", null, null, start, start.AddHours(-1), false));

        (await response.ReadProblemAsync(HttpStatusCode.BadRequest)).Code().ShouldBe("calendar.ends_before_start");
    }

    [Fact]
    public async Task Shopping_lists_track_items_and_their_state()
    {
        var client = factory.CreateClientFor(TestUser.Create("Owner"));
        var family = await client.CreateFamilyAsync();
        var lists = $"/v1/families/{family.Id}/shopping-lists";

        var list = await (await client.PostAsJsonAsync(lists, new SaveShoppingListRequest("Groceries"))).ReadAsync<ShoppingListResponse>(HttpStatusCode.Created);
        var milk = await (await client.PostAsJsonAsync($"{lists}/{list.Id}/items", new SaveShoppingListItemRequest("Milk", "2 L", false)))
            .ReadAsync<ShoppingListItemResponse>(HttpStatusCode.Created);
        await client.PostAsJsonAsync($"{lists}/{list.Id}/items", new SaveShoppingListItemRequest("Bread", null, false));

        var ticked = await (await client.SendAsync(ApiTestExtensions.Put($"{lists}/{list.Id}/items/{milk.Id}", new SaveShoppingListItemRequest("Milk", "2 L", true)).WithIfMatch(milk.ETag)))
            .ReadAsync<ShoppingListItemResponse>();
        ticked.IsChecked.ShouldBeTrue();

        var detail = await (await client.GetAsync($"{lists}/{list.Id}")).ReadAsync<ShoppingListResponse>();
        detail.Items.Select(i => i.Name).ShouldBe(["Bread", "Milk"], "unchecked items come first");

        var summaries = await (await client.GetAsync(lists)).ReadAsync<CursorPage<ShoppingListSummary>>();
        var summary = summaries.Items.ShouldHaveSingleItem();
        summary.ItemCount.ShouldBe(2);
        summary.OpenItemCount.ShouldBe(1);

        (await client.DeleteAsync($"{lists}/{list.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"{lists}/{list.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Items_cannot_be_reached_through_another_familys_route()
    {
        var alice = factory.CreateClientFor(TestUser.Create("Alice"));
        var bob = factory.CreateClientFor(TestUser.Create("Bob"));
        var aliceFamily = await alice.CreateFamilyAsync();
        var bobFamily = await bob.CreateFamilyAsync();

        var aliceList = await (await alice.PostAsJsonAsync($"/v1/families/{aliceFamily.Id}/shopping-lists", new SaveShoppingListRequest("Private")))
            .ReadAsync<ShoppingListResponse>(HttpStatusCode.Created);

        // Bob is authorized for his own family, but Alice's list must not be reachable through it.
        (await bob.GetAsync($"/v1/families/{bobFamily.Id}/shopping-lists/{aliceList.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await bob.PostAsJsonAsync($"/v1/families/{bobFamily.Id}/shopping-lists/{aliceList.Id}/items", new SaveShoppingListItemRequest("Sneaky", null, false)))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Meal_plans_return_entries_in_the_requested_range()
    {
        var client = factory.CreateClientFor(TestUser.Create("Owner"));
        var family = await client.CreateFamilyAsync();
        var plan = $"/v1/families/{family.Id}/meal-plan";
        var monday = new DateOnly(2030, 1, 7);

        await client.PostAsJsonAsync($"{plan}/entries", new SaveMealPlanEntryRequest(monday, MealType.Dinner, "Tacos", null));
        await client.PostAsJsonAsync($"{plan}/entries", new SaveMealPlanEntryRequest(monday, MealType.Breakfast, "Porridge", "Add berries"));
        await client.PostAsJsonAsync($"{plan}/entries", new SaveMealPlanEntryRequest(monday.AddDays(10), MealType.Lunch, "Soup", null));

        var week = await (await client.GetAsync($"{plan}?from=2030-01-07&to=2030-01-13")).ReadAsync<MealPlanResponse>();

        week.Entries.Select(e => e.Title).ShouldBe(["Porridge", "Tacos"]);
        (await client.GetAsync($"{plan}?from=2030-01-07&to=2030-06-01")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Notes_are_paginated_newest_first()
    {
        var client = factory.CreateClientFor(TestUser.Create("Owner"));
        var family = await client.CreateFamilyAsync();
        var notes = $"/v1/families/{family.Id}/notes";
        foreach (var title in new[] { "First", "Second", "Third" })
        {
            await client.PostAsJsonAsync(notes, new SaveNoteRequest(title, $"{title} note body"));
        }

        var page1 = await (await client.GetAsync($"{notes}?limit=2")).ReadAsync<CursorPage<NoteSummary>>();
        page1.Items.Select(n => n.Title).ShouldBe(["Third", "Second"]);

        var page2 = await (await client.GetAsync($"{notes}?limit=2&cursor={page1.NextCursor}")).ReadAsync<CursorPage<NoteSummary>>();
        page2.Items.ShouldHaveSingleItem().Title.ShouldBe("First");
        page2.NextCursor.ShouldBeNull();

        (await client.GetAsync($"{notes}?cursor=not-a-cursor")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
