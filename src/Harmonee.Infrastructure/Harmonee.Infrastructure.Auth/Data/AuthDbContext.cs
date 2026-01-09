using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

using Harmonee.Domain.Models.Auth;

namespace Harmonee.Infrastructure.Auth.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : IdentityDbContext<HarmoneeUser>(options);