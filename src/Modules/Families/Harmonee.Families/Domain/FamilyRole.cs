using Harmonee.Api.Contracts.Common;

namespace Harmonee.Families.Domain;

/// <summary>A family-defined role: a named set of permissions. Exactly one role per family is the default for people who join.</summary>
internal sealed class FamilyRole
{
    public static readonly IReadOnlyList<FamilyPermission> ViewEverything =
    [
        FamilyPermission.ViewCalendar,
        FamilyPermission.ViewShoppingLists,
        FamilyPermission.ViewMealPlans,
        FamilyPermission.ViewNotes,
    ];

    public static readonly IReadOnlyList<FamilyPermission> EditEverything =
    [
        .. ViewEverything,
        FamilyPermission.ManageCalendar,
        FamilyPermission.ManageShoppingLists,
        FamilyPermission.ManageMealPlans,
        FamilyPermission.ManageNotes,
    ];

    private FamilyRole()
    {
    }

    public FamilyRole(Guid familyId, string name, IEnumerable<FamilyPermission> permissions, bool isDefault)
    {
        Id = Guid.CreateVersion7();
        FamilyId = familyId;
        Name = name;
        Permissions = Normalize(permissions);
        IsDefault = isDefault;
    }

    public Guid Id { get; private set; }

    public Guid FamilyId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public List<FamilyPermission> Permissions { get; private set; } = [];

    public bool IsDefault { get; private set; }

    /// <summary>De-duplicates, sorts, and adds implied permissions: managing something implies viewing it.</summary>
    public static List<FamilyPermission> Normalize(IEnumerable<FamilyPermission> permissions)
    {
        var set = permissions.Where(Enum.IsDefined).ToHashSet();
        AddImplied(set, FamilyPermission.ManageCalendar, FamilyPermission.ViewCalendar);
        AddImplied(set, FamilyPermission.ManageShoppingLists, FamilyPermission.ViewShoppingLists);
        AddImplied(set, FamilyPermission.ManageMealPlans, FamilyPermission.ViewMealPlans);
        AddImplied(set, FamilyPermission.ManageNotes, FamilyPermission.ViewNotes);
        return [.. set.Order()];

        static void AddImplied(HashSet<FamilyPermission> set, FamilyPermission manage, FamilyPermission view)
        {
            if (set.Contains(manage))
            {
                set.Add(view);
            }
        }
    }

    internal void Update(string name, IEnumerable<FamilyPermission> permissions)
    {
        Name = name;
        Permissions = Normalize(permissions);
    }

    internal void SetDefault(bool isDefault) => IsDefault = isDefault;
}
