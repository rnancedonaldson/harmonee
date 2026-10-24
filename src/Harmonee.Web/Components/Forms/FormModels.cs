using System.ComponentModel.DataAnnotations;
using Harmonee.Api.Contracts.Calendar;
using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Api.Contracts.Kitchen;
using Harmonee.Api.Contracts.Notes;
using Harmonee.Web.Services;

namespace Harmonee.Web.Components.Forms;

// Mutable form models bound by Blazor's static SSR form handling. Validation mirrors the API's limits
// (FieldLimits) for fast feedback; the API remains the authority.

public sealed class CreateFamilyForm
{
    [Required(ErrorMessage = "Give your family a name.")]
    [StringLength(FieldLimits.NameMaxLength)]
    public string? Name { get; set; }
}

public sealed class JoinFamilyForm
{
    [Required(ErrorMessage = "Enter the 8-character invite code.")]
    [StringLength(FieldLimits.InviteCodeLength, MinimumLength = FieldLimits.InviteCodeLength, ErrorMessage = "Invite codes are 8 characters.")]
    public string? InviteCode { get; set; }
}

public sealed class EventForm
{
    [Required(ErrorMessage = "Give the event a title.")]
    [StringLength(FieldLimits.TitleMaxLength)]
    public string? Title { get; set; }

    [StringLength(FieldLimits.DescriptionMaxLength)]
    public string? Description { get; set; }

    [StringLength(FieldLimits.LocationMaxLength)]
    public string? Location { get; set; }

    public DateTime Starts { get; set; }

    public DateTime Ends { get; set; }

    public bool IsAllDay { get; set; }

    public string? ETag { get; set; }

    public static EventForm StartingAt(DateTime start) => new() { Starts = start, Ends = start.AddHours(1) };

    public static EventForm From(CalendarEventResponse e, UserTimeZone zone)
    {
        var ends = zone.ToLocal(e.EndsAt).DateTime;
        return new EventForm
        {
            Title = e.Title,
            Description = e.Description,
            Location = e.Location,
            Starts = zone.ToLocal(e.StartsAt).DateTime,
            Ends = e.IsAllDay ? ends.AddDays(-1) : ends,
            IsAllDay = e.IsAllDay,
            ETag = e.ETag,
        };
    }

    /// <summary>All-day events span whole local days: from the start date's midnight to the midnight after the end date.</summary>
    public SaveCalendarEventRequest ToRequest(UserTimeZone zone)
    {
        var starts = IsAllDay ? zone.StartOfDay(DateOnly.FromDateTime(Starts)) : zone.FromLocal(Starts);
        var ends = IsAllDay ? zone.StartOfDay(DateOnly.FromDateTime(Ends).AddDays(1)) : zone.FromLocal(Ends);
        return new SaveCalendarEventRequest(Title!.Trim(), Description, Location, starts, ends, IsAllDay);
    }
}

public sealed class ShoppingListForm
{
    [Required(ErrorMessage = "Give the list a name.")]
    [StringLength(FieldLimits.NameMaxLength)]
    public string? Name { get; set; }

    public string? ETag { get; set; }
}

public sealed class ShoppingItemForm
{
    [Required(ErrorMessage = "What do you need?")]
    [StringLength(FieldLimits.TitleMaxLength)]
    public string? Name { get; set; }

    [StringLength(FieldLimits.QuantityMaxLength)]
    public string? Quantity { get; set; }
}

/// <summary>Posted by the per-item forms on a shopping list (tick/untick, remove).</summary>
public sealed class ShoppingItemAction
{
    public Guid ItemId { get; set; }

    public string? ETag { get; set; }

    public bool Check { get; set; }
}

public sealed class MealForm
{
    public DateOnly Date { get; set; }

    public MealType MealType { get; set; } = MealType.Dinner;

    [Required(ErrorMessage = "What's for this meal?")]
    [StringLength(FieldLimits.TitleMaxLength)]
    public string? Title { get; set; }

    [StringLength(FieldLimits.MealNotesMaxLength)]
    public string? Notes { get; set; }

    public string? ETag { get; set; }

    public static MealForm From(MealPlanEntryResponse e) => new()
    {
        Date = e.Date,
        MealType = e.MealType,
        Title = e.Title,
        Notes = e.Notes,
        ETag = e.ETag,
    };

    public SaveMealPlanEntryRequest ToRequest() => new(Date, MealType, Title!.Trim(), Notes);
}

public sealed class NoteForm
{
    [Required(ErrorMessage = "Give the note a title.")]
    [StringLength(FieldLimits.TitleMaxLength)]
    public string? Title { get; set; }

    [StringLength(FieldLimits.NoteContentMaxLength)]
    public string? Content { get; set; }

    public string? ETag { get; set; }

    public SaveNoteRequest ToRequest() => new(Title!.Trim(), Content ?? string.Empty);
}

public sealed class FamilySettingsForm
{
    [Required(ErrorMessage = "Give your family a name.")]
    [StringLength(FieldLimits.NameMaxLength)]
    public string? Name { get; set; }

    public Guid DefaultRoleId { get; set; }
}

/// <summary>Posted by each member row on the settings page.</summary>
public sealed class MemberForm
{
    public Guid MemberId { get; set; }

    public Guid RoleId { get; set; }

    public bool IsOwner { get; set; }
}

/// <summary>A role editor. One boolean per permission keeps form binding trivial and robust.</summary>
public sealed class RoleForm
{
    public Guid? RoleId { get; set; }

    [Required(ErrorMessage = "Give the role a name.")]
    [StringLength(FieldLimits.NameMaxLength)]
    public string? Name { get; set; }

    public bool ViewCalendar { get; set; }

    public bool ManageCalendar { get; set; }

    public bool ViewShoppingLists { get; set; }

    public bool ManageShoppingLists { get; set; }

    public bool ViewMealPlans { get; set; }

    public bool ManageMealPlans { get; set; }

    public bool ViewNotes { get; set; }

    public bool ManageNotes { get; set; }

    public bool ManageMembers { get; set; }

    public bool ManageRoles { get; set; }

    public bool Get(FamilyPermission permission) => permission switch
    {
        FamilyPermission.ViewCalendar => ViewCalendar,
        FamilyPermission.ManageCalendar => ManageCalendar,
        FamilyPermission.ViewShoppingLists => ViewShoppingLists,
        FamilyPermission.ManageShoppingLists => ManageShoppingLists,
        FamilyPermission.ViewMealPlans => ViewMealPlans,
        FamilyPermission.ManageMealPlans => ManageMealPlans,
        FamilyPermission.ViewNotes => ViewNotes,
        FamilyPermission.ManageNotes => ManageNotes,
        FamilyPermission.ManageMembers => ManageMembers,
        FamilyPermission.ManageRoles => ManageRoles,
        _ => false,
    };

    public static RoleForm From(FamilyRoleResponse role) => new()
    {
        RoleId = role.Id,
        Name = role.Name,
        ViewCalendar = role.Permissions.Contains(FamilyPermission.ViewCalendar),
        ManageCalendar = role.Permissions.Contains(FamilyPermission.ManageCalendar),
        ViewShoppingLists = role.Permissions.Contains(FamilyPermission.ViewShoppingLists),
        ManageShoppingLists = role.Permissions.Contains(FamilyPermission.ManageShoppingLists),
        ViewMealPlans = role.Permissions.Contains(FamilyPermission.ViewMealPlans),
        ManageMealPlans = role.Permissions.Contains(FamilyPermission.ManageMealPlans),
        ViewNotes = role.Permissions.Contains(FamilyPermission.ViewNotes),
        ManageNotes = role.Permissions.Contains(FamilyPermission.ManageNotes),
        ManageMembers = role.Permissions.Contains(FamilyPermission.ManageMembers),
        ManageRoles = role.Permissions.Contains(FamilyPermission.ManageRoles),
    };

    public SaveRoleRequest ToRequest() =>
        new(Name!.Trim(), [.. FamilyPermissions.All.Where(Get)]);
}

/// <summary>Posted by delete buttons.</summary>
public sealed class DeleteForm
{
    public Guid Id { get; set; }
}

public static class FormValidation
{
    /// <summary>Validates a model bound from a plain form (EditForm does this automatically).</summary>
    public static string? FirstError(object model)
    {
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true)
            ? null
            : results[0].ErrorMessage;
    }
}
