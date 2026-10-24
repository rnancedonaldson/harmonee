using Harmonee.Api.Contracts.Common;
using Harmonee.Notes.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Harmonee.Notes.Infrastructure;

internal sealed class NotesDbContext(DbContextOptions<NotesDbContext> options) : DbContext(options)
{
    public const string Schema = "notes";

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var n = modelBuilder.Entity<Note>();
        n.ToTable("Notes");
        n.HasKey(x => x.Id);
        n.Property(x => x.Id).ValueGeneratedNever();
        n.Property(x => x.Title).HasMaxLength(FieldLimits.TitleMaxLength).IsRequired();
        n.Property(x => x.Content).HasMaxLength(FieldLimits.NoteContentMaxLength).IsRequired();
        n.Property(x => x.Version).IsRowVersion();
        n.HasIndex(x => new { x.FamilyId, x.UpdatedAt, x.Id });
    }
}

/// <summary>Lets <c>dotnet ef</c> create the context without starting the API host.</summary>
internal sealed class NotesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NotesDbContext>
{
    public NotesDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<NotesDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Harmonee;Trusted_Connection=True", sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", NotesDbContext.Schema))
            .Options);
}
