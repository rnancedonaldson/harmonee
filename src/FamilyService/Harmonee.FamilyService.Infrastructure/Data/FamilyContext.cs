using System;
using Harmonee.FamilyService.Domain.Models;
using Harmonee.Shared.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.FamilyService.Infrastructure.Data;

public class FamilyContext(DbContextOptions<FamilyContext> options) : HarmoneeContext(options)
{
    public DbSet<Family> Families { get; set; }
    public DbSet<FamilyMember> FamilyMembers { get; set; }
    
}
