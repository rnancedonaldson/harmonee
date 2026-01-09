using System.ComponentModel.DataAnnotations;

namespace Harmonee.Domain.Models.Auth;

public class HarmoneeRole : OwnedEntity
{
    [Key]
    public Guid RoleId;
    [StringLength(63, MinimumLength = 3)]
    public required string RoleName;
}
