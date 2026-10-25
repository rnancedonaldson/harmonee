namespace Harmonee.Families.Domain;

/// <summary>A user's membership in a family. Owners have every permission regardless of role.</summary>
internal sealed class FamilyMember
{
    private FamilyMember()
    {
    }

    public FamilyMember(Guid familyId, Guid userId, Guid roleId, bool isOwner, DateTimeOffset joinedAt)
    {
        Id = Guid.CreateVersion7();
        FamilyId = familyId;
        UserId = userId;
        RoleId = roleId;
        IsOwner = isOwner;
        JoinedAt = joinedAt;
    }

    public Guid Id { get; private set; }

    public Guid FamilyId { get; private set; }

    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public bool IsOwner { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    internal void Update(Guid roleId, bool isOwner)
    {
        RoleId = roleId;
        IsOwner = isOwner;
    }
}
