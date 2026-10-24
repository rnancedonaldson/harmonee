using System.ComponentModel.DataAnnotations;
using Harmonee.Api.Contracts.Common;

namespace Harmonee.Api.Contracts.Calendar;

public sealed record CalendarEventResponse(
    Guid Id,
    string Title,
    string? Description,
    string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ETag);

public sealed record CalendarEventsResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<CalendarEventResponse> Items,
    bool IsTruncated);

public sealed record SaveCalendarEventRequest(
    [Required, StringLength(FieldLimits.TitleMaxLength, MinimumLength = 1)] string Title,
    [StringLength(FieldLimits.DescriptionMaxLength)] string? Description,
    [StringLength(FieldLimits.LocationMaxLength)] string? Location,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsAllDay);
