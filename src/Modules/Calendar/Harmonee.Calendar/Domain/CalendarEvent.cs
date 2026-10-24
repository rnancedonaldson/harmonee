using Harmonee.SharedKernel;

namespace Harmonee.Calendar.Domain;

internal sealed class CalendarEvent
{
    public static readonly Error EndsBeforeStart = Error.Validation(
        "calendar.ends_before_start", "The event can't end before it starts.", "endsAt");

    public static readonly Error NotFound = Error.NotFound("calendar.event_not_found", "The event was not found.");

    private CalendarEvent()
    {
    }

    public Guid Id { get; private set; }

    public Guid FamilyId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Location { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public bool IsAllDay { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static Result<CalendarEvent> Create(
        Guid familyId, string title, string? description, string? location,
        DateTimeOffset startsAt, DateTimeOffset endsAt, bool isAllDay, Guid userId, DateTimeOffset now)
    {
        var calendarEvent = new CalendarEvent
        {
            Id = Guid.CreateVersion7(),
            FamilyId = familyId,
            CreatedByUserId = userId,
            CreatedAt = now,
        };

        var updated = calendarEvent.Update(title, description, location, startsAt, endsAt, isAllDay, userId, now);
        return updated.IsSuccess ? calendarEvent : updated.Error!;
    }

    public Result Update(
        string title, string? description, string? location,
        DateTimeOffset startsAt, DateTimeOffset endsAt, bool isAllDay, Guid userId, DateTimeOffset now)
    {
        if (endsAt < startsAt)
        {
            return EndsBeforeStart;
        }

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        StartsAt = startsAt.ToUniversalTime();
        EndsAt = endsAt.ToUniversalTime();
        IsAllDay = isAllDay;
        UpdatedByUserId = userId;
        UpdatedAt = now;
        return Result.Success();
    }
}
