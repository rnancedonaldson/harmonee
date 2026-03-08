using Microsoft.EntityFrameworkCore;

namespace Harmonee.Shared.Infrastructure.Models;

public class HarmoneeContext : DbContext
{
    public HarmoneeContext(DbContextOptions<HarmoneeContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
