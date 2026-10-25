using System.Diagnostics;
using Harmonee.SharedKernel.AspNetCore;

namespace Harmonee.MigrationService;

/// <summary>Runs each module's migrations in turn, ensures the app identities have database users, then stops the host. A failure exits non-zero so the API never starts on a bad schema.</summary>
internal sealed partial class MigrationWorker(
    IServiceProvider services,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger) : BackgroundService
{
    private static readonly ActivitySource s_activitySource = new("Harmonee.Migrations");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = s_activitySource.StartActivity("Migrate databases");
        try
        {
            await using var scope = services.CreateAsyncScope();
            foreach (var migrator in scope.ServiceProvider.GetServices<IModuleMigrator>())
            {
                LogMigrating(logger, migrator.Name);
                await migrator.MigrateAsync(stoppingToken);
            }

            await scope.ServiceProvider.GetRequiredService<DatabaseUserProvisioner>().EnsureAsync(stoppingToken);

            LogCompleted(logger);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            activity?.AddException(ex);
            LogFailed(logger, ex);
            Environment.ExitCode = 1;
        }

        lifetime.StopApplication();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying migrations for {DbContext}")]
    private static partial void LogMigrating(ILogger logger, string dbContext);

    [LoggerMessage(Level = LogLevel.Information, Message = "All migrations applied")]
    private static partial void LogCompleted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Database migration failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
