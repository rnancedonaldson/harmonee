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

internal static class MealPlanEndpoints
{
    private const int DefaultRangeDays = 7;

    public static void MapMealPlanEndpoints(this IEndpointRouteBuilder routes)
    {
        var plan = routes.MapGroup("/families/{familyId:guid}/meal-plan").WithTags("Meal plans");

        plan.MapGet("/", GetPlanAsync)
            .WithName("GetMealPlan")
            .WithSummary("Gets meal plan entries for an inclusive date range (default: 7 days from today, max 62 days)")
            .RequireFamilyPermission(FamilyPermission.ViewMealPlans);

        plan.MapPost("/entries", CreateAsync)
            .WithName("CreateMealPlanEntry")
            .RequireFamilyPermission(FamilyPermission.ManageMealPlans);

        plan.MapGet("/entries/{entryId:guid}", GetAsync)
            .WithName("GetMealPlanEntry")
            .RequireFamilyPermission(FamilyPermission.ViewMealPlans);

        plan.MapPut("/entries/{entryId:guid}", UpdateAsync)
            .WithName("UpdateMealPlanEntry")
            .WithDescription("Requires If-Match with the entry's ETag.")
            .RequireFamilyPermission(FamilyPermission.ManageMealPlans);

        plan.MapDelete("/entries/{entryId:guid}", DeleteAsync)
            .WithName("DeleteMealPlanEntry")
            .RequireFamilyPermission(FamilyPermission.ManageMealPlans);
    }

    private static async Task<Results<Ok<MealPlanResponse>, ProblemHttpResult>> GetPlanAsync(
        Guid familyId, DateOnly? from, DateOnly? to, KitchenDbContext db, TimeProvider clock, CancellationToken cancellationToken)
    {
        var start = from ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var end = to ?? start.AddDays(DefaultRangeDays - 1);
        if (end < start)
        {
            return Error.Validation("kitchen.invalid_range", "'to' can't be before 'from'.", "to").ToProblem();
        }

        if (end.DayNumber - start.DayNumber + 1 > FieldLimits.MaxMealPlanRangeDays)
        {
            return Error.Validation("kitchen.range_too_large", $"The range can be at most {FieldLimits.MaxMealPlanRangeDays} days.", "to").ToProblem();
        }

        var entries = await db.MealPlanEntries
            .Where(e => e.FamilyId == familyId && e.Date >= start && e.Date <= end)
            .OrderBy(e => e.Date).ThenBy(e => e.MealType).ThenBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new MealPlanResponse(start, end, [.. entries.Select(ToResponse)]));
    }

    private static async Task<Results<Ok<MealPlanEntryResponse>, ProblemHttpResult>> GetAsync(
        Guid familyId, Guid entryId, KitchenDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var entry = await db.MealPlanEntries.SingleOrDefaultAsync(e => e.FamilyId == familyId && e.Id == entryId, cancellationToken);
        if (entry is null)
        {
            return MealPlanEntry.NotFound.ToProblem();
        }

        var body = ToResponse(entry);
        response.Headers.ETag = body.ETag;
        return TypedResults.Ok(body);
    }

    private static async Task<Created<MealPlanEntryResponse>> CreateAsync(
        Guid familyId,
        SaveMealPlanEntryRequest request,
        CurrentFamilyAccess access,
        KitchenDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var entry = MealPlanEntry.Create(familyId, request.Date, request.MealType, request.Title, request.Notes, access.Value.UserId, clock.GetUtcNow());
        db.MealPlanEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/v1/families/{familyId}/meal-plan/entries/{entry.Id}", ToResponse(entry));
    }

    private static async Task<Results<Ok<MealPlanEntryResponse>, ProblemHttpResult>> UpdateAsync(
        Guid familyId,
        Guid entryId,
        SaveMealPlanEntryRequest request,
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

        var entry = await db.MealPlanEntries.AsTracking().SingleOrDefaultAsync(e => e.FamilyId == familyId && e.Id == entryId, cancellationToken);
        if (entry is null)
        {
            return MealPlanEntry.NotFound.ToProblem();
        }

        db.ExpectVersion(entry, expected.Value);
        entry.Update(request.Date, request.MealType, request.Title, request.Notes, access.Value.UserId, clock.GetUtcNow());
        var saved = await db.SaveWithConcurrencyCheckAsync("meal plan entry", cancellationToken);
        return saved.IsSuccess ? TypedResults.Ok(ToResponse(entry)) : saved.ToProblem();
    }

    private static async Task<NoContent> DeleteAsync(Guid familyId, Guid entryId, KitchenDbContext db, CancellationToken cancellationToken)
    {
        await db.MealPlanEntries.Where(e => e.FamilyId == familyId && e.Id == entryId).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static MealPlanEntryResponse ToResponse(MealPlanEntry e) => new(
        e.Id, e.Date, e.MealType, e.Title, e.Notes, e.CreatedByUserId, e.UpdatedAt, ETags.Format(e.Version));
}
