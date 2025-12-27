using System.Diagnostics;
using Harmonee.AuthService.Data;
using Harmonee.FamilyService.Data;
using Harmonee.KitchenService.Data;
using Harmonee.ScheduleService.Data;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.MigrationService;

public class Worker(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime hostApplicationLifetime) : BackgroundService
{
    private readonly ILogger<Worker> _logger = serviceProvider.GetRequiredService<ILogger<Worker>>();

    public const string ActivitySourceName = "Migrations";
    private static readonly ActivitySource s_activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var activity = s_activitySource.StartActivity("Migrating database", ActivityKind.Client);
        _logger.LogInformation("Beginning database migrations");
        await RunMigrationsAsync<HarmoneeAuthContext>(cancellationToken);
        await RunMigrationsAsync<FamilyContext>(cancellationToken);
        await RunMigrationsAsync<ScheduleContext>(cancellationToken);
        await RunMigrationsAsync<KitchenContext>(cancellationToken);

        hostApplicationLifetime.StopApplication();
    }

    protected async Task<bool> RunMigrationsAsync<T>(CancellationToken cancellationToken) where T : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<T>() as DbContext;
        return await MigrateAsync(dbContext, cancellationToken);
    }

    private async Task<bool> MigrateAsync<T>(T dbContext, CancellationToken cancellationToken) where T : DbContext
    {
        _logger.LogInformation("Migrating DbContext {dbContextName}", dbContext.GetType().Name);
        try
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // Run migration in a transaction to avoid partial migration if it fails.
                await dbContext.Database.MigrateAsync(cancellationToken);
            });
            _logger.LogInformation("Migrating DbContext {dbContextName} complete", dbContext.GetType().Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Migrating DbContext {dbContextName} failed with exception: {exception}", dbContext.GetType().Name, ex.ToString());
            return false;
        }
    }
}