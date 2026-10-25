using System.Globalization;

namespace Harmonee.Web.Services;

/// <summary>
/// Converts between UTC (how the API stores times) and the user's local time. The browser reports its IANA time zone
/// in a cookie (see wwwroot/js/timezone.js); until it has, times are shown in UTC.
/// </summary>
public sealed class UserTimeZone
{
    public const string CookieName = "harmonee_tz";

    public UserTimeZone(IHttpContextAccessor accessor)
    {
        var id = accessor.HttpContext?.Request.Cookies[CookieName];
        Zone = TryFind(id) ?? TimeZoneInfo.Utc;
    }

    public TimeZoneInfo Zone { get; }

    public bool IsUtcFallback => Zone.Equals(TimeZoneInfo.Utc);

    public DateTimeOffset Now(TimeProvider clock) => ToLocal(clock.GetUtcNow());

    public DateTimeOffset ToLocal(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Zone);

    public DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(Now(clock).DateTime);

    /// <summary>Interprets a wall-clock time entered by the user in their time zone.</summary>
    public DateTimeOffset FromLocal(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (Zone.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1); // Skipped by a DST transition.
        }

        return new DateTimeOffset(unspecified, Zone.GetUtcOffset(unspecified));
    }

    /// <summary>Start of the given local day, as an instant.</summary>
    public DateTimeOffset StartOfDay(DateOnly date) => FromLocal(date.ToDateTime(TimeOnly.MinValue));

    public static string Format(DateTimeOffset value, string format) => value.ToString(format, CultureInfo.CurrentCulture);

    private static TimeZoneInfo? TryFind(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
