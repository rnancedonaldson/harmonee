namespace Harmonee.SharedKernel.AspNetCore;

/// <summary>Applies one module's pending EF Core migrations. Registered automatically for every module DbContext.</summary>
public interface IModuleMigrator
{
    string Name { get; }

    Task MigrateAsync(CancellationToken cancellationToken);
}
