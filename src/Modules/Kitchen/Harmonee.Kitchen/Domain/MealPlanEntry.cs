using Harmonee.Api.Contracts.Kitchen;
using Harmonee.SharedKernel;

namespace Harmonee.Kitchen.Domain;

/// <summary>A planned meal on a given day.</summary>
internal sealed class MealPlanEntry
{
    public static readonly Error NotFound = Error.NotFound("kitchen.meal_not_found", "The meal plan entry was not found.");

    private MealPlanEntry()
    {
    }

    public Guid Id { get; private set; }

    public Guid FamilyId { get; private set; }

    public DateOnly Date { get; private set; }

    public MealType MealType { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static MealPlanEntry Create(Guid familyId, DateOnly date, MealType mealType, string title, string? notes, Guid userId, DateTimeOffset now)
    {
        var entry = new MealPlanEntry
        {
            Id = Guid.CreateVersion7(),
            FamilyId = familyId,
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        entry.Update(date, mealType, title, notes, userId, now);
        return entry;
    }

    public void Update(DateOnly date, MealType mealType, string title, string? notes, Guid userId, DateTimeOffset now)
    {
        Date = date;
        MealType = mealType;
        Title = title.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedByUserId = userId;
        UpdatedAt = now;
    }
}
