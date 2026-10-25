using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Harmonee.Web.Authentication;

/// <summary>Development-only sign-in: choose a display name (and optionally the platform admin role). No password; local only.</summary>
internal static class DevelopmentSignIn
{
    public const string PagePath = "/development/sign-in";
    public const string PlatformAdminRole = "Harmonee.Admin";

    // Form-bound minimal API endpoints require a valid antiforgery token by default.
    public static void Map(RouteGroupBuilder group) =>
        group.MapPost("/development/sign-in", SignInAsync);

    private static async Task<IResult> SignInAsync(
        [FromForm] string? name,
        [FromForm] bool? isAdmin,
        [FromForm] string? returnUrl,
        HttpContext context)
    {
        if (context.RequestServices.GetService<IApiAccessTokenProvider>() is not DevelopmentAccessTokenProvider)
        {
            return Results.NotFound();
        }

        var displayName = string.IsNullOrWhiteSpace(name) ? "Developer" : name.Trim()[..Math.Min(name.Trim().Length, 60)];
        List<Claim> claims =
        [
            new("oid", DevelopmentAccessTokenProvider.ObjectIdFor(displayName)),
            new("name", displayName),
        ];
        if (isAdmin == true)
        {
            claims.Add(new Claim("roles", PlatformAdminRole));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, "name", "roles");
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return Results.LocalRedirect(WebAuthentication.SafeReturnUrl(context, returnUrl));
    }
}
