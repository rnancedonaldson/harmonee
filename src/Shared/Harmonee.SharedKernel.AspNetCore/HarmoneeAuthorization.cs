namespace Harmonee.SharedKernel.AspNetCore;

/// <summary>Names shared between the identity provider configuration, the API, and the web app.</summary>
public static class HarmoneeAuthorization
{
    /// <summary>Delegated scope the API exposes. Clients must request it; the API rejects tokens without it.</summary>
    public const string AccessScope = "Harmonee.Access";

    /// <summary>App role (defined on the API app registration) for platform administrators.</summary>
    public const string PlatformAdminRole = "Harmonee.Admin";

    /// <summary>Authorization policy requiring the platform administrator role.</summary>
    public const string PlatformAdminPolicy = "platform-admin";
}
