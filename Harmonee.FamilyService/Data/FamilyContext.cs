using Microsoft.EntityFrameworkCore;

namespace Harmonee.FamilyService.Data;

public class FamilyContext : DbContext
{
    public FamilyContext(DbContextOptions<FamilyContext> options) : base(options) { }
}
