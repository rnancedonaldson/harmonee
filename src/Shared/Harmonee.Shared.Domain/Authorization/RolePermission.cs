namespace Harmonee.Shared.Domain.Authorization;

public record RolePermission<T>(string RoleName, Permission Permissions)
{
    public static RolePermission<T> Viewer<T>(string roleName)
        => new RolePermission<T>(roleName, Permission.Read);

    public static RolePermission<T> Editor<T>(string roleName)
        => new RolePermission<T>(roleName, Permission.Read | Permission.Create | Permission.Update);

    public static RolePermission<T> FamilyAdmin<T>(string roleName)
        => new RolePermission<T>(roleName, Permission.Read | Permission.Create | Permission.Update | Permission.Archive);

    public static RolePermission<T> SystemAdmin<T>(string roleName)
        => new RolePermission<T>(roleName, Permission.Read | Permission.Create | Permission.Update | Permission.Archive | Permission.Delete);
}