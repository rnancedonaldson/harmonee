using System;

namespace Harmonee.Domain.Shared.Models.Auth;

public abstract class OwnedEntity
{
    public required Guid Owner;
    public DateTimeOffset CreatedDate { get; } = DateTimeOffset.Now;
}
