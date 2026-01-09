using Harmonee.Domain.Shared.Models.Auth;
using Microsoft.AspNetCore.Identity;

namespace Harmonee.Domain.Models.Auth;

public class HarmoneeUser : IdentityUser<Guid>, IHarmoneeUser
{
    public Guid UserId => Id;
    public required string DisplayName { get; set; }
}