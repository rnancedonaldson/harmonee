using Harmonee.Api.Contracts.Calendar;
using Harmonee.Api.Contracts.Common;
using Harmonee.Calendar.Domain;
using Harmonee.Calendar.Infrastructure;
using Harmonee.Families.Contracts;
using Harmonee.SharedKernel;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.Calendar.Features;

internal static class EventEndpoints
{
    private const string Resource = "event";
    private static readonly TimeSpan s_defaultRange = TimeSpan.FromDays(31);

    public static void MapEventEndpoints(this IEndpointRouteBuilder routes)
    {
        var events = routes.MapGroup("/families/{familyId:guid}/events").WithTags("Calendar");

        events.MapGet("/", ListAsync)
            .WithName("ListEvents")
            .WithSummary("Lists events overlapping a time range (default: the next 31 days, max 93 days)")
            .RequireFamilyPermission(FamilyPermission.ViewCalendar);

        events.MapGet("/{eventId:guid}", GetAsync)
            .WithName("GetEvent")
            .RequireFamilyPermission(FamilyPermission.ViewCalendar);

        events.MapPost("/", CreateAsync)
            .WithName("CreateEvent")
            .RequireFamilyPermission(FamilyPermission.ManageCalendar);

        events.MapPut("/{eventId:guid}", UpdateAsync)
            .WithName("UpdateEvent")
            .WithDescription("Requires If-Match with the event's ETag.")
            .RequireFamilyPermission(FamilyPermission.ManageCalendar);

        events.MapDelete("/{eventId:guid}", DeleteAsync)
            .WithName("DeleteEvent")
            .WithDescription("If-Match is optional; when supplied the delete only succeeds if the event is unchanged.")
            .RequireFamilyPermission(FamilyPermission.ManageCalendar);
    }

    private static async Task<Results<Ok<CalendarEventsResponse>, ProblemHttpResult>> ListAsync(
        Guid familyId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CalendarDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var start = (from ?? new DateTimeOffset(clock.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero)).ToUniversalTime();
        var end = (to ?? start + s_defaultRange).ToUniversalTime();
        if (end <= start)
        {
            return Error.Validation("calendar.invalid_range", "'to' must be after 'from'.", "to").ToProblem();
        }

        if (end - start > TimeSpan.FromDays(FieldLimits.MaxCalendarRangeDays))
        {
            return Error.Validation("calendar.range_too_large", $"The range can be at most {FieldLimits.MaxCalendarRangeDays} days.", "to").ToProblem();
        }

        var events = await db.Events
            .Where(e => e.FamilyId == familyId && e.StartsAt < end && e.EndsAt >= start)
            .OrderBy(e => e.StartsAt).ThenBy(e => e.Id)
            .Take(FieldLimits.MaxCalendarEventsPerQuery + 1)
            .ToListAsync(cancellationToken);

        var truncated = events.Count > FieldLimits.MaxCalendarEventsPerQuery;
        var items = events.Take(FieldLimits.MaxCalendarEventsPerQuery).Select(ToResponse).ToList();
        return TypedResults.Ok(new CalendarEventsResponse(start, end, items, truncated));
    }

    private static async Task<Results<Ok<CalendarEventResponse>, ProblemHttpResult>> GetAsync(
        Guid familyId, Guid eventId, CalendarDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var calendarEvent = await db.Events.SingleOrDefaultAsync(e => e.FamilyId == familyId && e.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            return CalendarEvent.NotFound.ToProblem();
        }

        var body = ToResponse(calendarEvent);
        response.Headers.ETag = body.ETag;
        return TypedResults.Ok(body);
    }

    private static async Task<Results<Created<CalendarEventResponse>, ProblemHttpResult>> CreateAsync(
        Guid familyId,
        SaveCalendarEventRequest request,
        CurrentFamilyAccess access,
        CalendarDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var created = CalendarEvent.Create(
            familyId, request.Title, request.Description, request.Location,
            request.StartsAt, request.EndsAt, request.IsAllDay, access.Value.UserId, clock.GetUtcNow());
        if (created.IsFailure)
        {
            return created.ToProblem();
        }

        db.Events.Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/v1/families/{familyId}/events/{created.Value.Id}", ToResponse(created.Value));
    }

    private static async Task<Results<Ok<CalendarEventResponse>, ProblemHttpResult>> UpdateAsync(
        Guid familyId,
        Guid eventId,
        SaveCalendarEventRequest request,
        CurrentFamilyAccess access,
        CalendarDbContext db,
        TimeProvider clock,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var expected = ETags.FromIfMatch(httpRequest);
        if (expected.IsFailure)
        {
            return expected.ToProblem();
        }

        var calendarEvent = await db.Events.AsTracking().SingleOrDefaultAsync(e => e.FamilyId == familyId && e.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            return CalendarEvent.NotFound.ToProblem();
        }

        db.ExpectVersion(calendarEvent, expected.Value);
        var result = calendarEvent.Update(
            request.Title, request.Description, request.Location,
            request.StartsAt, request.EndsAt, request.IsAllDay, access.Value.UserId, clock.GetUtcNow());
        if (result.IsSuccess)
        {
            result = await db.SaveWithConcurrencyCheckAsync(Resource, cancellationToken);
        }

        return result.IsSuccess ? TypedResults.Ok(ToResponse(calendarEvent)) : result.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid familyId, Guid eventId, CalendarDbContext db, HttpRequest httpRequest, CancellationToken cancellationToken)
    {
        var calendarEvent = await db.Events.AsTracking().SingleOrDefaultAsync(e => e.FamilyId == familyId && e.Id == eventId, cancellationToken);
        if (calendarEvent is null)
        {
            return TypedResults.NoContent();
        }

        if (httpRequest.Headers.IfMatch.Count > 0)
        {
            var expected = ETags.FromIfMatch(httpRequest);
            if (expected.IsFailure)
            {
                return expected.ToProblem();
            }

            db.ExpectVersion(calendarEvent, expected.Value);
        }

        db.Events.Remove(calendarEvent);
        var saved = await db.SaveWithConcurrencyCheckAsync(Resource, cancellationToken);
        return saved.IsSuccess ? TypedResults.NoContent() : saved.ToProblem();
    }

    private static CalendarEventResponse ToResponse(CalendarEvent e) => new(
        e.Id, e.Title, e.Description, e.Location, e.StartsAt, e.EndsAt, e.IsAllDay,
        e.CreatedByUserId, e.CreatedAt, e.UpdatedAt, ETags.Format(e.Version));
}
