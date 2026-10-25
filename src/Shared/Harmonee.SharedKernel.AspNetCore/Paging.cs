using Harmonee.Api.Contracts.Common;

namespace Harmonee.SharedKernel.AspNetCore;

public static class Paging
{
    /// <summary>Clamps a client-supplied page size to the server's allowed range.</summary>
    public static int ClampLimit(int? limit) =>
        Math.Clamp(limit ?? FieldLimits.DefaultPageSize, 1, FieldLimits.MaxPageSize);

    public static Result<KeysetCursor?> ParseCursor(string? cursor)
    {
        if (string.IsNullOrEmpty(cursor))
        {
            return Result<KeysetCursor?>.Success(null);
        }

        return KeysetCursor.TryDecode(cursor, out var parsed)
            ? Result<KeysetCursor?>.Success(parsed)
            : Error.Validation("paging.invalid_cursor", "The cursor is not valid.", "cursor");
    }
}
