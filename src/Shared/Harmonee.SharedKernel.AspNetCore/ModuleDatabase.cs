using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Harmonee.SharedKernel.AspNetCore;

/// <summary>Registers module-owned DbContexts against the shared database, each in its own schema.</summary>
public static class ModuleDatabase
{
    public static IHostApplicationBuilder AddModuleDbContext<TContext>(this IHostApplicationBuilder builder, string schema)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHarmoneeModuleServices();
        builder.Services.AddDbContextPool<TContext>(options => options
            .UseSqlServer(
                builder.Configuration.GetConnectionString(ResourceNames.Database),
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", schema))
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        // Aspire enrichment: connection resiliency, health check, and OpenTelemetry tracing/metrics.
        builder.EnrichSqlServerDbContext<TContext>();

        // Lets the migration service (and tests) apply this module's migrations without seeing its internals.
        builder.Services.AddTransient<IModuleMigrator, ModuleMigrator<TContext>>();
        return builder;
    }

    /// <summary>Tells EF Core which row version the client last saw, so SaveChanges fails if it has changed since.</summary>
    public static void ExpectVersion<TEntity>(this DbContext db, TEntity entity, byte[] version)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(db);
        db.Entry(entity).Property("Version").OriginalValue = version;
    }

    private sealed class ModuleMigrator<TContext>(TContext db) : IModuleMigrator
        where TContext : DbContext
    {
        public string Name => typeof(TContext).Name;

        public Task MigrateAsync(CancellationToken cancellationToken) =>
            db.Database.CreateExecutionStrategy().ExecuteAsync(ct => db.Database.MigrateAsync(ct), cancellationToken);
    }

    /// <summary>Saves changes, translating an optimistic concurrency conflict into an expected failure.</summary>
    public static async Task<Result> SaveWithConcurrencyCheckAsync(this DbContext db, string resource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return ETags.Stale(resource);
        }
    }
}
