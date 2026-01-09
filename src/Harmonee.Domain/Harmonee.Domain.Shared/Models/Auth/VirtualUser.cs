using Harmonee.Domain.Models.Auth;

namespace Harmonee.Domain.Shared.Models.Auth;

public class VirtualUser : OwnedEntity, IHarmoneeUser
{
    private readonly Guid _id = Guid.NewGuid();
    public Guid UserId => _id;
    public string DisplayName { get; set; } = string.Empty;
}