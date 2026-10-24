using Harmonee.SharedKernel;

namespace Harmonee.Notes.Domain;

internal sealed class Note
{
    public const int ExcerptLength = 200;

    public static readonly Error NotFound = Error.NotFound("notes.note_not_found", "The note was not found.");

    private Note()
    {
    }

    public Guid Id { get; private set; }

    public Guid FamilyId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public byte[] Version { get; private set; } = [];

    public static Note Create(Guid familyId, string title, string content, Guid userId, DateTimeOffset now)
    {
        var note = new Note
        {
            Id = Guid.CreateVersion7(),
            FamilyId = familyId,
            CreatedByUserId = userId,
            CreatedAt = now,
        };
        note.Update(title, content, userId, now);
        return note;
    }

    public void Update(string title, string content, Guid userId, DateTimeOffset now)
    {
        Title = title.Trim();
        Content = content;
        UpdatedByUserId = userId;
        UpdatedAt = now;
    }
}
