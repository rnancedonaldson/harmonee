using System.ComponentModel.DataAnnotations.Schema;
using Harmonee.Domain.Shared.Models.Auth;

namespace Harmonee.Domain.Models.Auth;

public abstract class OwnedEntity
{
    [ForeignKey(nameof(IHarmoneeUser.UserId))]
    public required Guid Owner;
    public DateTimeOffset CreatedDate { get; } = DateTimeOffset.Now;
}
