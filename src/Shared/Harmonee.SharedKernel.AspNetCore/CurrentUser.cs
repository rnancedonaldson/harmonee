using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Harmonee.SharedKernel.AspNetCore;

/// <summary>The authenticated caller, as described by their access token.</summary>
public interface ICurrentUser
{
    /// <summary>Stable identifier of the user in the identity provider (the <c>oid</c> claim).</summary>
    string ObjectId { get; }

    /// <summary>Identity provider tenant (the <c>tid</c> claim).</summary>
    string TenantId { get; }

    string? DisplayName { get; }

    string? Email { get; }

    bool IsPlatformAdmin { get; }
}

/// <summary><see cref="ICurrentUser"/> backed by the current HTTP request's principal.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    // Claim types appear either short ("oid") or mapped to long URIs depending on inbound claim mapping.
    private const string ObjectIdLong = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    private const string TenantIdLong = "http://schemas.microsoft.com/identity/claims/tenantid";

    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
            ? user
            : throw new InvalidOperationException("There is no authenticated user for the current request.");

    public string ObjectId => Find("oid", ObjectIdLong) ?? throw new InvalidOperationException("The access token has no object ID (oid) claim.");

    public string TenantId => Find("tid", TenantIdLong) ?? string.Empty;

    public string? DisplayName => Find("name", ClaimTypes.Name);

    public string? Email => Find("email", ClaimTypes.Email) ?? Find("preferred_username", "upn");

    public bool IsPlatformAdmin => Principal.IsInRole(HarmoneeAuthorization.PlatformAdminRole);

    private string? Find(string shortType, string longType) =>
        Principal.FindFirstValue(shortType) ?? Principal.FindFirstValue(longType);
}
