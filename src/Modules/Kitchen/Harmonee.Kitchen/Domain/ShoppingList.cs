using Harmonee.SharedKernel;

namespace Harmonee.Kitchen.Domain;

internal sealed class ShoppingList
{
    public const int MaxItems = 500;

    public static readonly Error NotFound = Error.NotFound("kitchen.list_not_found", "The shopping list was not found.");
    public static readonly Error ItemLimitReached = Error.Conflict("kitchen.item_limit", $"A shopping list can have at most {MaxItems} items.");

    private ShoppingList()
    {
    }

    public Guid Id { get; private set; }

    public Guid FamilyId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static ShoppingList Create(Guid familyId, string name, Guid userId, DateTimeOffset now)
    {
        var list = new ShoppingList
        {
            Id = Guid.CreateVersion7(),
            FamilyId = familyId,
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        list.Rename(name, userId, now);
        return list;
    }

    public void Rename(string name, Guid userId, DateTimeOffset now)
    {
        Name = name.Trim();
        UpdatedByUserId = userId;
        UpdatedAt = now;
    }
}

/// <summary>An item on a shopping list. Versioned independently so people can tick items off concurrently.</summary>
internal sealed class ShoppingListItem
{
    public static readonly Error NotFound = Error.NotFound("kitchen.item_not_found", "The item was not found.");

    private ShoppingListItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid ListId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Quantity { get; private set; }

    public bool IsChecked { get; private set; }

    public Guid AddedByUserId { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static ShoppingListItem Create(Guid listId, string name, string? quantity, bool isChecked, Guid userId, DateTimeOffset now)
    {
        var item = new ShoppingListItem
        {
            Id = Guid.CreateVersion7(),
            ListId = listId,
            AddedByUserId = userId,
            AddedAt = now,
        };
        item.Update(name, quantity, isChecked, now);
        return item;
    }

    public void Update(string name, string? quantity, bool isChecked, DateTimeOffset now)
    {
        Name = name.Trim();
        Quantity = string.IsNullOrWhiteSpace(quantity) ? null : quantity.Trim();
        IsChecked = isChecked;
        UpdatedAt = now;
    }
}
