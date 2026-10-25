namespace Harmonee.Web.Authentication;

/// <summary>
/// Checks the credentials the web app uses to prove its own identity to Entra External ID (<c>AzureAd:ClientCredentials</c>,
/// read by Microsoft.Identity.Web). In Azure the app presents a token from its user-assigned managed identity
/// (<c>SignedAssertionFromManagedIdentity</c>, trusted through a federated identity credential on the app registration),
/// so there is no secret to store or rotate. Client secrets are only accepted in the Development environment.
/// </summary>
internal static class WebClientCredentials
{
    public const string SectionPath = "AzureAd:ClientCredentials";
    public const string ManagedIdentitySourceType = "SignedAssertionFromManagedIdentity";
    public const string ClientSecretSourceType = "ClientSecret";

    /// <summary>Set on every Azure container to the client ID of its user-assigned managed identity.</summary>
    public const string AzureClientIdKey = "AZURE_CLIENT_ID";

    /// <summary>
    /// Validates the configured credentials and returns any values to fill in. A managed identity credential without a
    /// <c>ManagedIdentityClientId</c> uses <c>AZURE_CLIENT_ID</c>; otherwise Microsoft.Identity.Web would look for a
    /// system-assigned identity, which the container apps don't have.
    /// </summary>
    public static Dictionary<string, string?> Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var credentials = configuration.GetSection(SectionPath).GetChildren().ToList();
        if (credentials.Count == 0)
        {
            throw new InvalidOperationException(
                $"{SectionPath} is empty. In Azure, configure a {ManagedIdentitySourceType} credential; locally, run through the AppHost with a client secret in its user secrets.");
        }

        var resolved = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var credential in credentials)
        {
            var sourceType = credential["SourceType"];
            if (string.Equals(sourceType, ClientSecretSourceType, StringComparison.OrdinalIgnoreCase) && !environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"{SectionPath} uses a client secret, which is only allowed in the Development environment. Use {ManagedIdentitySourceType} (see docs/deployment.md).");
            }

            if (string.Equals(sourceType, ManagedIdentitySourceType, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(credential["ManagedIdentityClientId"])
                && configuration[AzureClientIdKey] is { Length: > 0 } clientId)
            {
                resolved[$"{credential.Path}:ManagedIdentityClientId"] = clientId;
            }
        }

        return resolved;
    }
}
