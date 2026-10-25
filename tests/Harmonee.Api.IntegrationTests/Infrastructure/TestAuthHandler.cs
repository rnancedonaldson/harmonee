using System.Security.Claims;
using System.Text.Encodings.Web;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmonee.Api.IntegrationTests.Infrastructure;

/// <summary>A signed-in user for tests. Each test creates its own users, so tests never share data.</summary>
public sealed record TestUser(string ObjectId, string Name, bool IsPlatformAdmin = false)
{
    public const string TenantId = "11111111-1111-1111-1111-111111111111";

    public static TestUser Create(string name, bool isPlatformAdmin = false) =>
        new(Guid.NewGuid().ToString(), name, isPlatformAdmin);
}

/// <summary>Builds the same claims an Entra External ID access token would carry, from request headers.</summary>
internal sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";
    public const string NameHeader = "X-Test-Name";
    public const string AdminHeader = "X-Test-Admin";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserHeader, out var objectId))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        List<Claim> claims =
        [
            new("oid", objectId.ToString()),
            new("tid", TestUser.TenantId),
            new("name", Request.Headers[NameHeader].ToString()),
            new("scp", HarmoneeAuthorization.AccessScope),
        ];

        if (Request.Headers.ContainsKey(AdminHeader))
        {
            claims.Add(new Claim(ClaimTypes.Role, HarmoneeAuthorization.PlatformAdminRole));
        }

        var identity = new ClaimsIdentity(claims, SchemeName, "name", ClaimTypes.Role);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
