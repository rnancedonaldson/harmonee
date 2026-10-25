using Harmonee.Api.Contracts.Calendar;
using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Web.Components.Forms;
using Harmonee.Web.Services;
using Microsoft.AspNetCore.Http;

namespace Harmonee.Web.Tests;

public sealed class FormModelTests
{
    private static UserTimeZone ZoneFor(string? ianaId)
    {
        var context = new DefaultHttpContext();
        if (ianaId is not null)
        {
            context.Request.Headers.Cookie = $"{UserTimeZone.CookieName}={Uri.EscapeDataString(ianaId)}";
        }

        return new UserTimeZone(new HttpContextAccessor { HttpContext = context });
    }

    [Fact]
    public void Role_form_round_trips_permissions()
    {
        var role = new FamilyRoleResponse(Guid.NewGuid(), "Kids", [FamilyPermission.ViewCalendar, FamilyPermission.ManageShoppingLists], 0);

        var request = RoleForm.From(role).ToRequest();

        request.Name.ShouldBe("Kids");
        request.Permissions.ShouldBe([FamilyPermission.ViewCalendar, FamilyPermission.ManageShoppingLists], ignoreOrder: true);
    }

    [Fact]
    public void Local_event_times_are_converted_using_the_users_time_zone()
    {
        var zone = ZoneFor("America/New_York");
        var form = new EventForm { Title = "Dentist", Starts = new DateTime(2030, 7, 1, 9, 0, 0), Ends = new DateTime(2030, 7, 1, 10, 0, 0) };

        var request = form.ToRequest(zone);

        request.StartsAt.ShouldBe(new DateTimeOffset(2030, 7, 1, 13, 0, 0, TimeSpan.Zero), "9am EDT is 13:00 UTC");
        request.EndsAt.ShouldBe(new DateTimeOffset(2030, 7, 1, 14, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void All_day_events_span_whole_local_days_and_round_trip()
    {
        var zone = ZoneFor("Europe/London");
        var form = new EventForm { Title = "Holiday", Starts = new DateTime(2030, 8, 1), Ends = new DateTime(2030, 8, 3), IsAllDay = true };

        var request = form.ToRequest(zone);
        request.StartsAt.ShouldBe(new DateTimeOffset(2030, 7, 31, 23, 0, 0, TimeSpan.Zero), "midnight BST");
        request.EndsAt.ShouldBe(new DateTimeOffset(2030, 8, 3, 23, 0, 0, TimeSpan.Zero), "the end date is inclusive");

        var response = new CalendarEventResponse(Guid.NewGuid(), "Holiday", null, null, request.StartsAt, request.EndsAt, true, Guid.NewGuid(), default, default, "\"x\"");
        var edited = EventForm.From(response, zone);
        edited.Starts.Date.ShouldBe(new DateTime(2030, 8, 1));
        edited.Ends.Date.ShouldBe(new DateTime(2030, 8, 3));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Not/AZone")]
    public void Unknown_time_zones_fall_back_to_utc(string? ianaId) =>
        ZoneFor(ianaId).IsUtcFallback.ShouldBeTrue();

    [Fact]
    public void Plain_form_validation_reports_the_first_error()
    {
        FormValidation.FirstError(new RoleForm { Name = "" }).ShouldBe("Give the role a name.");
        FormValidation.FirstError(new RoleForm { Name = "Kids" }).ShouldBeNull();
        FormValidation.FirstError(new RoleForm { Name = new string('x', FieldLimits.NameMaxLength + 1) }).ShouldNotBeNull();
    }

    [Fact]
    public void Day_labels_are_relative_to_today()
    {
        var today = new DateOnly(2030, 1, 7);

        Display.Day(today, today).ShouldBe("Today");
        Display.Day(today.AddDays(1), today).ShouldBe("Tomorrow");
    }
}
