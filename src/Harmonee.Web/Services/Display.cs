using System.Globalization;
using Harmonee.Api.Contracts.Calendar;

namespace Harmonee.Web.Services;

/// <summary>Formatting helpers for dates and times shown in the UI.</summary>
public static class Display
{
    public static string EventWhen(CalendarEventResponse e, UserTimeZone zone)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(zone);

        var start = zone.ToLocal(e.StartsAt);
        if (e.IsAllDay)
        {
            return start.ToString("ddd d MMM", CultureInfo.CurrentCulture) + " · all day";
        }

        var end = zone.ToLocal(e.EndsAt);
        var sameDay = start.Date == end.Date;
        return sameDay
            ? $"{start.ToString("ddd d MMM, HH:mm", CultureInfo.CurrentCulture)}–{end.ToString("HH:mm", CultureInfo.CurrentCulture)}"
            : $"{start.ToString("ddd d MMM, HH:mm", CultureInfo.CurrentCulture)} – {end.ToString("ddd d MMM, HH:mm", CultureInfo.CurrentCulture)}";
    }

    public static string Time(DateTimeOffset value, UserTimeZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return zone.ToLocal(value).ToString("HH:mm", CultureInfo.CurrentCulture);
    }

    public static string DateTime(DateTimeOffset value, UserTimeZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return zone.ToLocal(value).ToString("d MMM yyyy, HH:mm", CultureInfo.CurrentCulture);
    }

    public static string Day(DateOnly date, DateOnly today)
    {
        if (date == today)
        {
            return "Today";
        }

        return date == today.AddDays(1) ? "Tomorrow" : date.ToString("ddd d MMM", CultureInfo.CurrentCulture);
    }

    public static string LongDay(DateOnly date) => date.ToString("dddd d MMMM", CultureInfo.CurrentCulture);

    public static string Iso(DateOnly date) => date.ToString("O", CultureInfo.InvariantCulture);
}
