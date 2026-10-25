using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Harmonee.Api.Contracts.Common;

namespace Harmonee.Api.Contracts.Kitchen;

[JsonConverter(typeof(JsonStringEnumConverter<MealType>))]
public enum MealType
{
    Breakfast,
    Lunch,
    Dinner,
    Snack,
}

public sealed record ShoppingListSummary(
    Guid Id,
    string Name,
    int ItemCount,
    int OpenItemCount,
    DateTimeOffset UpdatedAt,
    string ETag);

public sealed record ShoppingListResponse(
    Guid Id,
    string Name,
    IReadOnlyList<ShoppingListItemResponse> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ETag);

public sealed record ShoppingListItemResponse(
    Guid Id,
    string Name,
    string? Quantity,
    bool IsChecked,
    Guid AddedByUserId,
    DateTimeOffset AddedAt,
    string ETag);

public sealed record SaveShoppingListRequest(
    [Required, StringLength(FieldLimits.NameMaxLength, MinimumLength = 1)] string Name);

public sealed record SaveShoppingListItemRequest(
    [Required, StringLength(FieldLimits.TitleMaxLength, MinimumLength = 1)] string Name,
    [StringLength(FieldLimits.QuantityMaxLength)] string? Quantity,
    bool IsChecked);

public sealed record MealPlanEntryResponse(
    Guid Id,
    DateOnly Date,
    MealType MealType,
    string Title,
    string? Notes,
    Guid CreatedByUserId,
    DateTimeOffset UpdatedAt,
    string ETag);

public sealed record MealPlanResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<MealPlanEntryResponse> Entries);

public sealed record SaveMealPlanEntryRequest(
    DateOnly Date,
    MealType MealType,
    [Required, StringLength(FieldLimits.TitleMaxLength, MinimumLength = 1)] string Title,
    [StringLength(FieldLimits.MealNotesMaxLength)] string? Notes);
