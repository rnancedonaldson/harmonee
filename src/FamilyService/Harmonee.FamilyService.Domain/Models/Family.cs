using System;
using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.FamilyService.Domain.Models;

public class Family : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string FamilyName { get; set; } = string.Empty;
    
}
