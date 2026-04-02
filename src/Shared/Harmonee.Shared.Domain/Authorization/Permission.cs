namespace Harmonee.Shared.Domain.Authorization;

[Flags]
public enum Permission
{
    None = 0,
    Read = 1,
    Create = 2,
    Update = 4,
    Archive = 8,
    Delete = 16
}
