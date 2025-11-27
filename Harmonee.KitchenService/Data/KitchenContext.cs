using Microsoft.EntityFrameworkCore;

namespace Harmonee.KitchenService.Data;

public class KitchenContext : DbContext
{
    public KitchenContext(DbContextOptions<KitchenContext> options) : base(options) { }
}
