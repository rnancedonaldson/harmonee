using Harmonee.Api.Contracts.Common;
using Harmonee.Families.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Harmonee.Families.Infrastructure;

internal sealed class FamiliesDbContext(DbContextOptions<FamiliesDbContext> options) : DbContext(options)
{
    public const string Schema = "families";

    public DbSet<Family> Families => Set<Family>();

    public DbSet<FamilyMember> Members => Set<FamilyMember>();

    public DbSet<FamilyRole> Roles => Set<FamilyRole>();

    public DbSet<UserProfile> Users => Set<UserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new FamilyConfiguration());
        modelBuilder.ApplyConfiguration(new FamilyMemberConfiguration());
        modelBuilder.ApplyConfiguration(new FamilyRoleConfiguration());
        modelBuilder.ApplyConfiguration(new UserProfileConfiguration());
    }

    private sealed class FamilyConfiguration : IEntityTypeConfiguration<Family>
    {
        public void Configure(EntityTypeBuilder<Family> b)
        {
            b.ToTable("Families");
            b.HasKey(f => f.Id);
            b.Property(f => f.Id).ValueGeneratedNever();
            b.Property(f => f.Name).HasMaxLength(FieldLimits.NameMaxLength).IsRequired();
            b.Property(f => f.InviteCode).HasMaxLength(FieldLimits.InviteCodeLength).IsFixedLength().IsUnicode(false).IsRequired();
            b.HasIndex(f => f.InviteCode).IsUnique();
            b.HasIndex(f => new { f.CreatedAt, f.Id });
            b.Property(f => f.Version).IsRowVersion();
            b.Ignore(f => f.DefaultRole);

            b.HasMany(f => f.Members).WithOne().HasForeignKey(m => m.FamilyId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(f => f.Roles).WithOne().HasForeignKey(r => r.FamilyId).OnDelete(DeleteBehavior.Cascade);
        }
    }

    private sealed class FamilyMemberConfiguration : IEntityTypeConfiguration<FamilyMember>
    {
        public void Configure(EntityTypeBuilder<FamilyMember> b)
        {
            b.ToTable("Members");
            b.HasKey(m => m.Id);
            b.Property(m => m.Id).ValueGeneratedNever();
            b.HasIndex(m => new { m.FamilyId, m.UserId }).IsUnique();
            b.HasIndex(m => m.UserId);
            b.HasOne<FamilyRole>().WithMany().HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<UserProfile>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    private sealed class FamilyRoleConfiguration : IEntityTypeConfiguration<FamilyRole>
    {
        public void Configure(EntityTypeBuilder<FamilyRole> b)
        {
            b.ToTable("Roles");
            b.HasKey(r => r.Id);
            b.Property(r => r.Id).ValueGeneratedNever();
            b.Property(r => r.Name).HasMaxLength(FieldLimits.NameMaxLength).IsRequired();
            b.HasIndex(r => new { r.FamilyId, r.Name }).IsUnique();
            b.PrimitiveCollection(r => r.Permissions)
                .HasMaxLength(2000)
                .ElementType(e => e.HasConversion<string>());
        }
    }

    private sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> b)
        {
            b.ToTable("Users");
            b.HasKey(u => u.Id);
            b.Property(u => u.Id).ValueGeneratedNever();
            b.Property(u => u.ObjectId).HasMaxLength(64).IsUnicode(false).IsRequired();
            b.Property(u => u.TenantId).HasMaxLength(64).IsUnicode(false).IsRequired();
            b.HasIndex(u => new { u.TenantId, u.ObjectId }).IsUnique();
            b.Property(u => u.DisplayName).HasMaxLength(UserProfile.DisplayNameMaxLength).IsRequired();
            b.Property(u => u.Email).HasMaxLength(UserProfile.EmailMaxLength);
        }
    }
}

/// <summary>Lets <c>dotnet ef</c> create the context without starting the API host.</summary>
internal sealed class FamiliesDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FamiliesDbContext>
{
    public FamiliesDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<FamiliesDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Harmonee;Trusted_Connection=True", sql =>
                sql.MigrationsHistoryTable("__EFMigrationsHistory", FamiliesDbContext.Schema))
            .Options);
}
