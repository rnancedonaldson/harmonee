using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Harmonee.SharedKernel.AspNetCore;

/// <summary>Strong ETags derived from database row versions, used for optimistic concurrency.</summary>
public static class ETags
{
    public static string Format(byte[] version) => $"\"{Convert.ToBase64String(version)}\"";

    /// <summary>Reads the row version the client expects from the <c>If-Match</c> header.</summary>
    public static Result<byte[]> FromIfMatch(HttpRequest request)
    {
        var header = request.Headers[HeaderNames.IfMatch].ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return new Error(
                "concurrency.if_match_required",
                "Updates require an If-Match header containing the resource's ETag.",
                ErrorKind.PreconditionRequired);
        }

        var value = header.Trim();
        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        value = value.Trim('"');
        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written == 0)
        {
            return Error.Validation("concurrency.invalid_etag", "The If-Match header is not a valid ETag.", HeaderNames.IfMatch);
        }

        return buffer[..written];
    }

    public static Error Stale(string resource) => new(
        "concurrency.stale",
        $"The {resource} was changed by someone else. Reload it and try again.",
        ErrorKind.PreconditionFailed);
}
