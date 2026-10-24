using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Harmonee.Api.Contracts.Common;

/// <summary>A page of results from a keyset-paginated collection. Pass <see cref="NextCursor"/> back to fetch the next page.</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? NextCursor);

/// <summary>Maximum field lengths and limits. Shared so clients can validate before calling the API.</summary>
public static class FieldLimits
{
    public const int NameMaxLength = 100;
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int LocationMaxLength = 200;
    public const int QuantityMaxLength = 50;
    public const int NoteContentMaxLength = 10_000;
    public const int MealNotesMaxLength = 1000;
    public const int InviteCodeLength = 8;
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
    public const int MaxCalendarRangeDays = 93;
    public const int MaxMealPlanRangeDays = 62;
    public const int MaxCalendarEventsPerQuery = 500;
}

/// <summary>Stable problem "type" URIs returned in RFC 9457 Problem Details. Clients branch on these, never on titles.</summary>
public static class ProblemTypes
{
    private const string Base = "https://harmonee.app/problems/";

    public static string For(string code) => Base + code.Replace('.', '-');
}

/// <summary>A permission that a family role can grant. Owners and platform administrators have every permission.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FamilyPermission>))]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Domain language: a family permission.")]
public enum FamilyPermission
{
    ViewCalendar,
    ManageCalendar,
    ViewShoppingLists,
    ManageShoppingLists,
    ViewMealPlans,
    ManageMealPlans,
    ViewNotes,
    ManageNotes,
    ManageMembers,
    ManageRoles,
}

/// <summary>Describes permissions for display in clients.</summary>
public static class FamilyPermissions
{
    public static IReadOnlyList<FamilyPermission> All { get; } = Enum.GetValues<FamilyPermission>();

    public static string Describe(FamilyPermission permission) => permission switch
    {
        FamilyPermission.ViewCalendar => "View calendar events",
        FamilyPermission.ManageCalendar => "Create, edit and delete calendar events",
        FamilyPermission.ViewShoppingLists => "View shopping lists",
        FamilyPermission.ManageShoppingLists => "Create, edit and delete shopping lists and items",
        FamilyPermission.ViewMealPlans => "View meal plans",
        FamilyPermission.ManageMealPlans => "Create, edit and delete meal plan entries",
        FamilyPermission.ViewNotes => "View notes",
        FamilyPermission.ManageNotes => "Create, edit and delete notes",
        FamilyPermission.ManageMembers => "Change members' roles, remove members and manage the invite code",
        FamilyPermission.ManageRoles => "Create, edit and delete roles",
        _ => permission.ToString(),
    };
}
