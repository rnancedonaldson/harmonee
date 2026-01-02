using Harmonee.Core.Models.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace Harmonee.AuthService.Data;

public class HarmoneeAuthContext : IdentityDbContext<HarmoneeUser>
{
    public HarmoneeAuthContext(DbContextOptions<HarmoneeAuthContext> options) : base(options) { }
}
