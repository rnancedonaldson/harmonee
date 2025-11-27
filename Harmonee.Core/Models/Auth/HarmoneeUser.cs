using Microsoft.AspNetCore.Identity;

namespace Harmonee.Core.Models.Auth;

public class HarmoneeUser : IdentityUser
{
    string FirstName { get; set; }
    string LastName { get; set; }
}
