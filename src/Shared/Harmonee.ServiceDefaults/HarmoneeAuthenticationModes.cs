using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Harmonee.ServiceDefaults;

/// <summary>
/// Selects how users sign in. <c>EntraExternalId</c> is the real mode. <c>Development</c> lets developers run the whole
/// app locally before an identity provider is configured: the web app signs users in by name and mints short-lived
/// tokens with a per-run key that the API trusts. It is refused outside the Development environment.
/// </summary>
public static class HarmoneeAuthenticationModes
{
    public const string ConfigurationKey = "Authentication:Mode";
    public const string SigningKeyConfigurationKey = "Authentication:DevelopmentSigningKey";

    public const string EntraExternalId = "EntraExternalId";
    public const string Development = "Development";

    public const string DevelopmentIssuer = "https://harmonee.localhost/development";
    public const string DevelopmentAudience = "harmonee-api";
    public const string DevelopmentTenantId = "development";

    public static bool UseDevelopmentAuthentication(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var mode = builder.Configuration[ConfigurationKey] ?? EntraExternalId;
        if (string.Equals(mode, EntraExternalId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(mode, Development, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unknown {ConfigurationKey} '{mode}'. Use '{EntraExternalId}' or '{Development}'.");
        }

        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey}={Development} is only allowed in the Development environment. Configure Entra External ID instead.");
        }

        if (string.IsNullOrWhiteSpace(builder.Configuration[SigningKeyConfigurationKey]))
        {
            throw new InvalidOperationException(
                $"{ConfigurationKey}={Development} requires {SigningKeyConfigurationKey}. Run the app through the Aspire AppHost, which generates one.");
        }

        return true;
    }

    public static byte[] GetDevelopmentSigningKey(this IConfiguration configuration) =>
        Convert.FromBase64String(configuration[SigningKeyConfigurationKey]
            ?? throw new InvalidOperationException($"{SigningKeyConfigurationKey} is not configured."));
}
