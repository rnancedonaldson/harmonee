namespace Harmonee.Families.Domain;

/// <summary>
/// A person who has signed in. Created on first use from their identity-provider claims and refreshed as those change.
/// Internal IDs decouple Harmonee data from the identity provider's identifiers.
/// </summary>
internal sealed class UserProfile
{
    public const int DisplayNameMaxLength = 200;
    public const int EmailMaxLength = 320;
    private static readonly TimeSpan s_lastSeenResolution = TimeSpan.FromHours(1);

    private UserProfile()
    {
    }

    public UserProfile(string objectId, string tenantId, string? displayName, string? email, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7();
        ObjectId = objectId;
        TenantId = tenantId;
        DisplayName = CleanDisplayName(displayName, email);
        Email = Truncate(email, EmailMaxLength);
        CreatedAt = now;
        LastSeenAt = now;
    }

    public Guid Id { get; private set; }

    public string ObjectId { get; private set; } = string.Empty;

    public string TenantId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Refreshes details from the latest token. Returns true if anything changed and needs saving.</summary>
    public bool Refresh(string? displayName, string? email, DateTimeOffset now)
    {
        var newName = CleanDisplayName(displayName, email);
        var newEmail = Truncate(email, EmailMaxLength) ?? Email;
        var changed = !string.Equals(newName, DisplayName, StringComparison.Ordinal)
            || !string.Equals(newEmail, Email, StringComparison.Ordinal)
            || now - LastSeenAt > s_lastSeenResolution;

        if (changed)
        {
            DisplayName = newName;
            Email = newEmail;
            LastSeenAt = now;
        }

        return changed;
    }

    private static string CleanDisplayName(string? displayName, string? email)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? email?.Split('@')[0] : displayName.Trim();
        return Truncate(string.IsNullOrWhiteSpace(name) ? "Family member" : name, DisplayNameMaxLength)!;
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
