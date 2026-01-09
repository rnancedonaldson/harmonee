using System.ComponentModel.DataAnnotations;

namespace Harmonee.Domain.Shared.Models.Auth;

public interface IHarmoneeUser
{
    [Key]
    public Guid UserId { get; }
    [Required]
    [StringLength(31, MinimumLength = 2)]
    public string DisplayName { get; set; }
}
