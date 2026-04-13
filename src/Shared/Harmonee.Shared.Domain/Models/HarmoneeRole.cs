using System;
using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.Shared.Domain.Models;

public class HarmoneeRole : IEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
}
