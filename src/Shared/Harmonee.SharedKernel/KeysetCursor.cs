using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Harmonee.SharedKernel;

/// <summary>
/// Opaque cursor for keyset pagination ordered by (timestamp DESC, id DESC).
/// Clients treat the encoded value as opaque; only the server parses it.
/// </summary>
public readonly record struct KeysetCursor(DateTimeOffset Timestamp, Guid Id)
{
    public string Encode()
    {
        var raw = string.Create(CultureInfo.InvariantCulture, $"{Timestamp.UtcTicks}|{Id:N}");
        return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(raw));
    }

    public static bool TryDecode(string? value, out KeysetCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        try
        {
            var raw = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));
            var parts = raw.Split('|');
            if (parts.Length != 2
                || !long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                || !Guid.TryParseExact(parts[1], "N", out var id))
            {
                return false;
            }

            cursor = new KeysetCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
