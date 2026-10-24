using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Harmonee.ServiceDefaults;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Harmonee.Web.Authentication;

/// <summary>Produces the Authorization header value for calling the API as the signed-in user.</summary>
internal interface IApiAccessTokenProvider
{
    Task<string> GetAuthorizationHeaderAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
}

/// <summary>Gets the user's access token for the API from Microsoft.Identity.Web's server-side token cache.</summary>
internal sealed class EntraAccessTokenProvider(IAuthorizationHeaderProvider headers, IOptions<HarmoneeApiOptions> options) : IApiAccessTokenProvider
{
    public Task<string> GetAuthorizationHeaderAsync(ClaimsPrincipal user, CancellationToken cancellationToken) =>
        headers.CreateAuthorizationHeaderForUserAsync(options.Value.Scopes, claimsPrincipal: user, cancellationToken: cancellationToken);
}

/// <summary>Development only: mints a short-lived token for the signed-in development user, signed with the per-run key.</summary>
internal sealed class DevelopmentAccessTokenProvider(IConfiguration configuration, TimeProvider clock) : IApiAccessTokenProvider
{
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(configuration.GetDevelopmentSigningKey()), SecurityAlgorithms.HmacSha256);

    public Task<string> GetAuthorizationHeaderAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["oid"] = user.FindClaim("oid") ?? throw new InvalidOperationException("The development user has no oid claim."),
            ["tid"] = HarmoneeAuthenticationModes.DevelopmentTenantId,
            ["name"] = user.FindClaim("name") ?? "Developer",
            ["scp"] = "Harmonee.Access",
        };

        if (user.HasClaim("roles", DevelopmentSignIn.PlatformAdminRole))
        {
            claims["roles"] = new[] { DevelopmentSignIn.PlatformAdminRole };
        }

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = HarmoneeAuthenticationModes.DevelopmentIssuer,
            Audience = HarmoneeAuthenticationModes.DevelopmentAudience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(10),
            SigningCredentials = _credentials,
        });

        return Task.FromResult($"Bearer {token}");
    }

    /// <summary>A stable object ID per development user name, so signing in with the same name returns to the same data.</summary>
    public static string ObjectIdFor(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name.Trim().ToUpperInvariant()));
        return new Guid(hash.AsSpan(0, 16)).ToString();
    }
}
