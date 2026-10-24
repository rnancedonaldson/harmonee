using Harmonee.Api.Contracts.Common;
using Harmonee.Kitchen.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Harmonee.Kitchen.Infrastructure;

internal sealed class KitchenDbContext(DbContextOptions<KitchenDbContext> options) : DbContext(options)
{
    public const string Schema = "kitchen";

    public DbSet<ShoppingList> ShoppingLists => Set<ShoppingList>();

    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    public DbSet<MealPlanEntry> MealPlanEntries => Set<MealPlanEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var list = modelBuilder.Entity<ShoppingList>();
        list.ToTable("ShoppingLists");
        list.HasKey(x => x.Id);
        list.Property(x => x.Id).ValueGeneratedNever();
        list.Property(x => x.Name).HasMaxLength(FieldLimits.NameMaxLength).IsRequired();
        list.Property(x => x.Version).IsRowVersion();
        list.HasIndex(x => new { x.FamilyId, x.CreatedAt, x.Id });

        var item = modelBuilder.Entity<ShoppingListItem>();
        item.ToTable("ShoppingListItems");
        item.HasKey(x => x.Id);
        item.Property(x => x.Id).ValueGeneratedNever();
        item.Property(x => x.Name).HasMaxLength(FieldLimits.TitleMaxLength).IsRequired();
        item.Property(x => x.Quantity).HasMaxLength(FieldLimits.QuantityMaxLength);
        item.Property(x => x.Version).IsRowVersion();
        item.HasOne<ShoppingList>().WithMany().HasForeignKey(x => x.ListId).OnDelete(DeleteBehavior.Cascade);
        item.HasIndex(x => new { x.ListId, x.IsChecked, x.AddedAt });

        var meal = modelBuilder.Entity<MealPlanEntry>();
        meal.ToTable("MealPlanEntries");
        meal.HasKey(x => x.Id);
        meal.Property(x => x.Id).ValueGeneratedNever();
        meal.Property(x => x.MealType).HasConversion<string>().HasMaxLength(16);
        meal.Property(x => x.Title).HasMaxLength(FieldLimits.TitleMaxLength).IsRequired();
        meal.Property(x => x.Notes).HasMaxLength(FieldLimits.MealNotesMaxLength);
        meal.Property(x => x.Version).IsRowVersion();
        meal.HasIndex(x => new { x.FamilyId, x.Date });
    }
}

/// <summary>Lets <c>dotnet ef</c> create the context without starting the API host.</summary>
internal sealed class KitchenDbContextDesignTimeFactory : IDesignTimeDbContextFactory<KitchenDbContext>
{
    public KitchenDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<KitchenDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Harmonee;Trusted_Connection=True", sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", KitchenDbContext.Schema))
            .Options);
}
