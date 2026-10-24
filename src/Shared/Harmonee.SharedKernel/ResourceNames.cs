namespace Harmonee.SharedKernel;

/// <summary>Names of resources in the Aspire app model. Shared so the AppHost and services agree on connection names.</summary>
public static class ResourceNames
{
    public const string Database = "harmonee-db";
    public const string Api = "api";
    public const string Web = "web";
    public const string Migrations = "migrations";
}
