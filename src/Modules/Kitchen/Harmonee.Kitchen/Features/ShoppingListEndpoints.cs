using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Kitchen;
using Harmonee.Families.Contracts;
using Harmonee.Kitchen.Domain;
using Harmonee.Kitchen.Infrastructure;
using Harmonee.SharedKernel;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.Kitchen.Features;

internal static class ShoppingListEndpoints
{
    public static void MapShoppingListEndpoints(this IEndpointRouteBuilder routes)
    {
        var lists = routes.MapGroup("/families/{familyId:guid}/shopping-lists").WithTags("Shopping lists");

        lists.MapGet("/", ListAsync)
            .WithName("ListShoppingLists")
            .WithSummary("Lists shopping lists, newest first")
            .RequireFamilyPermission(FamilyPermission.ViewShoppingLists);

        lists.MapGet("/{listId:guid}", GetAsync)
            .WithName("GetShoppingList")
            .WithSummary("Gets a shopping list with its items (unchecked first)")
            .RequireFamilyPermission(FamilyPermission.ViewShoppingLists);

        lists.MapPost("/", CreateAsync)
            .WithName("CreateShoppingList")
            .RequireFamilyPermission(FamilyPermission.ManageShoppingLists);

        lists.MapPut("/{listId:guid}", RenameAsync)
            .WithName("RenameShoppingList")
            .WithDescription("Requires If-Match with the list's ETag.")
            .RequireFamilyPermission(FamilyPermission.ManageShoppingLists);

        lists.MapDelete("/{listId:guid}", DeleteAsync)
            .WithName("DeleteShoppingList")
            .WithDescription("Deletes the list and all of its items.")
            .RequireFamilyPermission(FamilyPermission.ManageShoppingLists);

        lists.MapPost("/{listId:guid}/items", AddItemAsync)
            .WithName("AddShoppingListItem")
            .RequireFamilyPermission(FamilyPermission.ManageShoppingLists);

        lists.MapPut("/{listId:guid}/items/{itemId:guid}", UpdateItemAsync)
            .WithName("UpdateShoppingListItem")
            .WithDescription("Requires If-Match with the item's ETag.")
            .RequireFamilyPermission(FamilyPermission.ManageShoppingLists);

        lists.MapDelete("/{listId:guid}/items/{itemId:guid}", DeleteItemAsync)
            .WithName("DeleteShoppingListItem")
            .RequireFamilyPermission(FamilyPermission.ManageShoppingLists);
    }

    private static async Task<Results<Ok<CursorPage<ShoppingListSummary>>, ProblemHttpResult>> ListAsync(
        Guid familyId, string? cursor, int? limit, KitchenDbContext db, CancellationToken cancellationToken)
    {
        var parsedCursor = Paging.ParseCursor(cursor);
        if (parsedCursor.IsFailure)
        {
            return parsedCursor.ToProblem();
        }

        var take = Paging.ClampLimit(limit);
        var query = db.ShoppingLists.Where(l => l.FamilyId == familyId);
        if (parsedCursor.Value is { } after)
        {
            query = query.Where(l => l.CreatedAt < after.Timestamp || (l.CreatedAt == after.Timestamp && l.Id.CompareTo(after.Id) < 0));
        }

        var page = await query
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Take(take + 1)
            .Select(l => new
            {
                l.Id,
                l.Name,
                l.CreatedAt,
                l.UpdatedAt,
                l.Version,
                ItemCount = db.ShoppingListItems.Count(i => i.ListId == l.Id),
                OpenItemCount = db.ShoppingListItems.Count(i => i.ListId == l.Id && !i.IsChecked),
            })
            .ToListAsync(cancellationToken);

        var visible = page.Take(take).ToList();
        var items = visible
            .Select(l => new ShoppingListSummary(l.Id, l.Name, l.ItemCount, l.OpenItemCount, l.UpdatedAt, ETags.Format(l.Version)))
            .ToList();
        var next = page.Count > take ? new KeysetCursor(visible[^1].CreatedAt, visible[^1].Id).Encode() : null;
        return TypedResults.Ok(new CursorPage<ShoppingListSummary>(items, next));
    }

    private static async Task<Results<Ok<ShoppingListResponse>, ProblemHttpResult>> GetAsync(
        Guid familyId, Guid listId, KitchenDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var list = await db.ShoppingLists.SingleOrDefaultAsync(l => l.FamilyId == familyId && l.Id == listId, cancellationToken);
        if (list is null)
        {
            return ShoppingList.NotFound.ToProblem();
        }

        var items = await db.ShoppingListItems
            .Where(i => i.ListId == listId)
            .OrderBy(i => i.IsChecked).ThenBy(i => i.AddedAt).ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);

        var body = ToResponse(list, items);
        response.Headers.ETag = body.ETag;
        return TypedResults.Ok(body);
    }

    private static async Task<Created<ShoppingListResponse>> CreateAsync(
        Guid familyId,
        SaveShoppingListRequest request,
        CurrentFamilyAccess access,
        KitchenDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var list = ShoppingList.Create(familyId, request.Name, access.Value.UserId, clock.GetUtcNow());
        db.ShoppingLists.Add(list);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/v1/families/{familyId}/shopping-lists/{list.Id}", ToResponse(list, []));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RenameAsync(
        Guid familyId,
        Guid listId,
        SaveShoppingListRequest request,
        CurrentFamilyAccess access,
        KitchenDbContext db,
        TimeProvider clock,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var expected = ETags.FromIfMatch(httpRequest);
        if (expected.IsFailure)
        {
            return expected.ToProblem();
        }

        var list = await db.ShoppingLists.AsTracking().SingleOrDefaultAsync(l => l.FamilyId == familyId && l.Id == listId, cancellationToken);
        if (list is null)
        {
            return ShoppingList.NotFound.ToProblem();
        }

        db.ExpectVersion(list, expected.Value);
        list.Rename(request.Name, access.Value.UserId, clock.GetUtcNow());
        var saved = await db.SaveWithConcurrencyCheckAsync("shopping list", cancellationToken);
        return saved.IsSuccess ? TypedResults.NoContent() : saved.ToProblem();
    }

    private static async Task<NoContent> DeleteAsync(Guid familyId, Guid listId, KitchenDbContext db, CancellationToken cancellationToken)
    {
        // Items cascade in the database.
        await db.ShoppingLists.Where(l => l.FamilyId == familyId && l.Id == listId).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Created<ShoppingListItemResponse>, ProblemHttpResult>> AddItemAsync(
        Guid familyId,
        Guid listId,
        SaveShoppingListItemRequest request,
        CurrentFamilyAccess access,
        KitchenDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!await db.ShoppingLists.AnyAsync(l => l.FamilyId == familyId && l.Id == listId, cancellationToken))
        {
            return ShoppingList.NotFound.ToProblem();
        }

        if (await db.ShoppingListItems.CountAsync(i => i.ListId == listId, cancellationToken) >= ShoppingList.MaxItems)
        {
            return ShoppingList.ItemLimitReached.ToProblem();
        }

        var item = ShoppingListItem.Create(listId, request.Name, request.Quantity, request.IsChecked, access.Value.UserId, clock.GetUtcNow());
        db.ShoppingListItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/v1/families/{familyId}/shopping-lists/{listId}/items/{item.Id}", ToResponse(item));
    }

    private static async Task<Results<Ok<ShoppingListItemResponse>, ProblemHttpResult>> UpdateItemAsync(
        Guid familyId,
        Guid listId,
        Guid itemId,
        SaveShoppingListItemRequest request,
        KitchenDbContext db,
        TimeProvider clock,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var expected = ETags.FromIfMatch(httpRequest);
        if (expected.IsFailure)
        {
            return expected.ToProblem();
        }

        var item = await FindItem(db, familyId, listId, itemId).AsTracking().SingleOrDefaultAsync(cancellationToken);
        if (item is null)
        {
            return ShoppingListItem.NotFound.ToProblem();
        }

        db.ExpectVersion(item, expected.Value);
        item.Update(request.Name, request.Quantity, request.IsChecked, clock.GetUtcNow());
        var saved = await db.SaveWithConcurrencyCheckAsync("item", cancellationToken);
        return saved.IsSuccess ? TypedResults.Ok(ToResponse(item)) : saved.ToProblem();
    }

    private static async Task<NoContent> DeleteItemAsync(
        Guid familyId, Guid listId, Guid itemId, KitchenDbContext db, CancellationToken cancellationToken)
    {
        await FindItem(db, familyId, listId, itemId).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Items are only reachable through a list that belongs to the authorized family.</summary>
    private static IQueryable<ShoppingListItem> FindItem(KitchenDbContext db, Guid familyId, Guid listId, Guid itemId) =>
        db.ShoppingListItems.Where(i => i.Id == itemId && i.ListId == listId
            && db.ShoppingLists.Any(l => l.Id == listId && l.FamilyId == familyId));

    private static ShoppingListResponse ToResponse(ShoppingList list, IEnumerable<ShoppingListItem> items) => new(
        list.Id, list.Name, [.. items.Select(ToResponse)], list.CreatedAt, list.UpdatedAt, ETags.Format(list.Version));

    private static ShoppingListItemResponse ToResponse(ShoppingListItem i) => new(
        i.Id, i.Name, i.Quantity, i.IsChecked, i.AddedByUserId, i.AddedAt, ETags.Format(i.Version));
}
