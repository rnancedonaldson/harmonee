using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Families.Infrastructure;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.Families.Features;

/// <summary>
/// Platform-administrator endpoints. Administrators reach family data through the regular family endpoints
/// (they receive admin-override access, which is audit-logged); this group adds cross-family discovery.
/// </summary>
internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var admin = routes.MapGroup("/admin")
            .WithTags("Admin")
            .RequireAuthorization(HarmoneeAuthorization.PlatformAdminPolicy);

        admin.MapGet("/families", ListFamiliesAsync)
            .WithName("AdminListFamilies")
            .WithSummary("Lists all families, newest first, optionally filtered by name");
    }

    private static async Task<Results<Ok<CursorPage<AdminFamilySummary>>, ProblemHttpResult>> ListFamiliesAsync(
        FamiliesDbContext db,
        string? search,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken)
    {
        var parsedCursor = Paging.ParseCursor(cursor);
        if (parsedCursor.IsFailure)
        {
            return parsedCursor.ToProblem();
        }

        var take = Paging.ClampLimit(limit);
        var query = db.Families.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            term = term.Length > FieldLimits.NameMaxLength ? term[..FieldLimits.NameMaxLength] : term;
            query = query.Where(f => f.Name.Contains(term));
        }

        if (parsedCursor.Value is { } after)
        {
            query = query.Where(f => f.CreatedAt < after.Timestamp || (f.CreatedAt == after.Timestamp && f.Id.CompareTo(after.Id) < 0));
        }

        var page = await query
            .OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.Id)
            .Take(take + 1)
            .Select(f => new
            {
                f.Id,
                f.Name,
                f.CreatedAt,
                MemberCount = f.Members.Count,
                OwnerNames = (from m in f.Members
                              join u in db.Users on m.UserId equals u.Id
                              where m.IsOwner
                              orderby u.DisplayName
                              select u.DisplayName).ToList(),
            })
            .ToListAsync(cancellationToken);

        var items = page.Take(take)
            .Select(f => new AdminFamilySummary(f.Id, f.Name, f.MemberCount, f.OwnerNames, f.CreatedAt))
            .ToList();
        var next = page.Count > take ? new SharedKernel.KeysetCursor(items[^1].CreatedAt, items[^1].Id).Encode() : null;

        return TypedResults.Ok(new CursorPage<AdminFamilySummary>(items, next));
    }
}
