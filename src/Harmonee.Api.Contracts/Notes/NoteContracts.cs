using System.ComponentModel.DataAnnotations;
using Harmonee.Api.Contracts.Common;

namespace Harmonee.Api.Contracts.Notes;

public sealed record NoteSummary(
    Guid Id,
    string Title,
    string Excerpt,
    Guid UpdatedByUserId,
    DateTimeOffset UpdatedAt);

public sealed record NoteResponse(
    Guid Id,
    string Title,
    string Content,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    Guid UpdatedByUserId,
    DateTimeOffset UpdatedAt,
    string ETag);

public sealed record SaveNoteRequest(
    [Required, StringLength(FieldLimits.TitleMaxLength, MinimumLength = 1)] string Title,
    [Required, StringLength(FieldLimits.NoteContentMaxLength)] string Content);
