using Harmonee.Domain.Shared.Models.Auth;

namespace Harmonee.Domain.Models.Family;

public class Family : OwnedEntity
{
    public IEnumerable<Guid> MemberIds;
    public string FamilyName;

    public Family(Guid ownerId, IEnumerable<Guid> memberIds, string familyName)
    {
        Owner = ownerId;
        MemberIds = memberIds;
        FamilyName = familyName;
    }
}