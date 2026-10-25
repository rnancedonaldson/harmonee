using System.Security.Claims;
using Azure.Identity;
using Harmonee.ServiceDefaults;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

namespace Harmonee.Web.Authentication;

/// <summary>
/// Backend-for-Frontend authentication. The web server is a confidential OIDC client: it signs users in with
/// Entra External ID, keeps tokens in a server-side cache, and gives the browser only an HTTP-only session cookie.
/// </summary>
internal static class WebAuthentication
{
    public const string ApiOptionsSection = "HarmoneeApi";
    public const string TokenCacheConnectionName = "cache";

    public static WebApplicationBuilder AddWebAuthentication(this WebApplicationBuilder builder)
    {
        var useDevelopment = builder.UseDevelopmentAuthentication();
        builder.Services.Configure<HarmoneeApiOptions>(builder.Configuration.GetSection(ApiOptionsSection));

        if (useDevelopment)
        {
            builder.Services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    ConfigureCookie(options);
                    options.LoginPath = DevelopmentSignIn.PagePath;
                });
            builder.Services.AddSingleton<IApiAccessTokenProvider, DevelopmentAccessTokenProvider>();
            return builder;
        }

        // Fill in credential defaults before Microsoft.Identity.Web binds the AzureAd section.
        builder.Configuration.AddInMemoryCollection(WebClientCredentials.Resolve(builder.Configuration, builder.Environment));

        var scopes = builder.Configuration.GetSection($"{ApiOptionsSection}:Scopes").Get<string[]>() ?? [];
        builder.Services
            .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
            .EnableTokenAcquisitionToCallDownstreamApi(scopes)
            .AddDistributedTokenCaches();

        builder.AddTokenCacheStore();
        builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            ConfigureCookie(options);
            options.Events.OnValidatePrincipal = context => RejectSessionWithoutTokensAsync(context, scopes);
        });
        builder.Services.AddScoped<IApiAccessTokenProvider, EntraAccessTokenProvider>();
        return builder;
    }

    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/authentication").AllowAnonymous();

        group.MapGet("/login", (string? returnUrl, HttpContext context) =>
            TypedResults.Challenge(new AuthenticationProperties { RedirectUri = SafeReturnUrl(context, returnUrl) }));

        group.MapPost("/logout", async (HttpContext context) =>
            {
                var properties = new AuthenticationProperties { RedirectUri = "/" };
                if (context.RequestServices.GetService<IApiAccessTokenProvider>() is DevelopmentAccessTokenProvider)
                {
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return Results.LocalRedirect("/");
                }

                return TypedResults.SignOut(properties, [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
            })
            .WithMetadata(new Microsoft.AspNetCore.Antiforgery.RequireAntiforgeryTokenAttribute());

        DevelopmentSignIn.Map(group);
        return routes;
    }

    /// <summary>Only allow redirects back into this app (prevents open redirects).</summary>
    public static string SafeReturnUrl(HttpContext context, string? returnUrl)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrEmpty(returnUrl))
        {
            return "/";
        }

        var isLocal = returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal) && !returnUrl.StartsWith("/\\", StringComparison.Ordinal);
        return isLocal ? returnUrl : "/";
    }

    /// <summary>
    /// Where the server-side token cache lives. With Redis configured (<c>ConnectionStrings:cache</c>), sessions survive
    /// restarts and work across replicas; otherwise tokens are kept in memory, which suits local development and a
    /// single replica. A connection string without a password authenticates with the app's managed identity (Entra ID).
    /// </summary>
    private static void AddTokenCacheStore(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(TokenCacheConnectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            builder.Services.AddDistributedMemoryCache();
            return;
        }

        var redis = builder.AddRedisClientBuilder(TokenCacheConnectionName);
        if (!connectionString.Contains("password=", StringComparison.OrdinalIgnoreCase))
        {
            redis.WithAzureAuthentication(new DefaultAzureCredential());
        }

        redis.WithDistributedCache();
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = "__Host-harmonee";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.AccessDeniedPath = "/access-denied";
    }

    /// <summary>
    /// If the server-side token cache no longer has tokens for this session (e.g. after a restart with an in-memory cache),
    /// end the session so the user signs in again instead of seeing API failures.
    /// </summary>
    private static async Task RejectSessionWithoutTokensAsync(CookieValidatePrincipalContext context, string[] scopes)
    {
        if (context.Principal is not { Identity.IsAuthenticated: true } principal || scopes.Length == 0)
        {
            return;
        }

        try
        {
            var tokens = context.HttpContext.RequestServices.GetRequiredService<ITokenAcquisition>();
            await tokens.GetAccessTokenForUserAsync(scopes, user: principal);
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            context.RejectPrincipal();
        }
    }

    public static string? FindClaim(this ClaimsPrincipal principal, string type) => principal.FindFirst(type)?.Value;
}

internal sealed class HarmoneeApiOptions
{
    public Uri BaseAddress { get; set; } = new("https+http://api");

    public IReadOnlyList<string> Scopes { get; set; } = [];
}
