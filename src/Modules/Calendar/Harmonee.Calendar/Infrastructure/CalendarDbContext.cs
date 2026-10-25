using Harmonee.Api.Contracts.Common;
using Harmonee.Calendar.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Harmonee.Calendar.Infrastructure;

internal sealed class CalendarDbContext(DbContextOptions<CalendarDbContext> options) : DbContext(options)
{
    public const string Schema = "calendar";

    public DbSet<CalendarEvent> Events => Set<CalendarEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var e = modelBuilder.Entity<CalendarEvent>();
        e.ToTable("Events");
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
        e.Property(x => x.Title).HasMaxLength(FieldLimits.TitleMaxLength).IsRequired();
        e.Property(x => x.Description).HasMaxLength(FieldLimits.DescriptionMaxLength);
        e.Property(x => x.Location).HasMaxLength(FieldLimits.LocationMaxLength);
        e.Property(x => x.Version).IsRowVersion();
        e.HasIndex(x => new { x.FamilyId, x.StartsAt });
    }
}

/// <summary>Lets <c>dotnet ef</c> create the context without starting the API host.</summary>
internal sealed class CalendarDbContextDesignTimeFactory : IDesignTimeDbContextFactory<CalendarDbContext>
{
    public CalendarDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Harmonee;Trusted_Connection=True", sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", CalendarDbContext.Schema))
            .Options);
}
