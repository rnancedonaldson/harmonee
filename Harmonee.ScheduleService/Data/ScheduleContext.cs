using Microsoft.EntityFrameworkCore;

namespace Harmonee.ScheduleService.Data;

public class ScheduleContext : DbContext
{
    public ScheduleContext(DbContextOptions<ScheduleContext> options) : base(options) { }
}
