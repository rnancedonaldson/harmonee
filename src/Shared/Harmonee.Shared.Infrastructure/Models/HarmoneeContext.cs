using Microsoft.EntityFrameworkCore;

namespace Harmonee.Shared.Infrastructure.Models;

public class HarmoneeContext : DbContext
{
    protected HarmoneeContext(DbContextOptions options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
