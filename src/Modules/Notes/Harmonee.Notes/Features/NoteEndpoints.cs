using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Notes;
using Harmonee.Families.Contracts;
using Harmonee.Notes.Domain;
using Harmonee.Notes.Infrastructure;
using Harmonee.SharedKernel;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Harmonee.Notes.Features;

internal static class NoteEndpoints
{
    private const string Resource = "note";

    public static void MapNoteEndpoints(this IEndpointRouteBuilder routes)
    {
        var notes = routes.MapGroup("/families/{familyId:guid}/notes").WithTags("Notes");

        notes.MapGet("/", ListAsync)
            .WithName("ListNotes")
            .WithSummary("Lists notes, most recently updated first")
            .RequireFamilyPermission(FamilyPermission.ViewNotes);

        notes.MapGet("/{noteId:guid}", GetAsync)
            .WithName("GetNote")
            .RequireFamilyPermission(FamilyPermission.ViewNotes);

        notes.MapPost("/", CreateAsync)
            .WithName("CreateNote")
            .RequireFamilyPermission(FamilyPermission.ManageNotes);

        notes.MapPut("/{noteId:guid}", UpdateAsync)
            .WithName("UpdateNote")
            .WithDescription("Requires If-Match with the note's ETag.")
            .RequireFamilyPermission(FamilyPermission.ManageNotes);

        notes.MapDelete("/{noteId:guid}", DeleteAsync)
            .WithName("DeleteNote")
            .RequireFamilyPermission(FamilyPermission.ManageNotes);
    }

    private static async Task<Results<Ok<CursorPage<NoteSummary>>, ProblemHttpResult>> ListAsync(
        Guid familyId, string? cursor, int? limit, NotesDbContext db, CancellationToken cancellationToken)
    {
        var parsedCursor = Paging.ParseCursor(cursor);
        if (parsedCursor.IsFailure)
        {
            return parsedCursor.ToProblem();
        }

        var take = Paging.ClampLimit(limit);
        var query = db.Notes.Where(n => n.FamilyId == familyId);
        if (parsedCursor.Value is { } after)
        {
            query = query.Where(n => n.UpdatedAt < after.Timestamp || (n.UpdatedAt == after.Timestamp && n.Id.CompareTo(after.Id) < 0));
        }

        var page = await query
            .OrderByDescending(n => n.UpdatedAt).ThenByDescending(n => n.Id)
            .Take(take + 1)
            .Select(n => new
            {
                n.Id,
                n.Title,
                Excerpt = n.Content.Substring(0, Note.ExcerptLength),
                n.UpdatedByUserId,
                n.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        var items = page.Take(take)
            .Select(n => new NoteSummary(n.Id, n.Title, n.Excerpt, n.UpdatedByUserId, n.UpdatedAt))
            .ToList();
        var next = page.Count > take ? new KeysetCursor(items[^1].UpdatedAt, items[^1].Id).Encode() : null;
        return TypedResults.Ok(new CursorPage<NoteSummary>(items, next));
    }

    private static async Task<Results<Ok<NoteResponse>, ProblemHttpResult>> GetAsync(
        Guid familyId, Guid noteId, NotesDbContext db, HttpResponse response, CancellationToken cancellationToken)
    {
        var note = await db.Notes.SingleOrDefaultAsync(n => n.FamilyId == familyId && n.Id == noteId, cancellationToken);
        if (note is null)
        {
            return Note.NotFound.ToProblem();
        }

        var body = ToResponse(note);
        response.Headers.ETag = body.ETag;
        return TypedResults.Ok(body);
    }

    private static async Task<Created<NoteResponse>> CreateAsync(
        Guid familyId,
        SaveNoteRequest request,
        CurrentFamilyAccess access,
        NotesDbContext db,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var note = Note.Create(familyId, request.Title, request.Content, access.Value.UserId, clock.GetUtcNow());
        db.Notes.Add(note);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/v1/families/{familyId}/notes/{note.Id}", ToResponse(note));
    }

    private static async Task<Results<Ok<NoteResponse>, ProblemHttpResult>> UpdateAsync(
        Guid familyId,
        Guid noteId,
        SaveNoteRequest request,
        CurrentFamilyAccess access,
        NotesDbContext db,
        TimeProvider clock,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var expected = ETags.FromIfMatch(httpRequest);
        if (expected.IsFailure)
        {
            return expected.ToProblem();
        }

        var note = await db.Notes.AsTracking().SingleOrDefaultAsync(n => n.FamilyId == familyId && n.Id == noteId, cancellationToken);
        if (note is null)
        {
            return Note.NotFound.ToProblem();
        }

        db.ExpectVersion(note, expected.Value);
        note.Update(request.Title, request.Content, access.Value.UserId, clock.GetUtcNow());
        var saved = await db.SaveWithConcurrencyCheckAsync(Resource, cancellationToken);
        return saved.IsSuccess ? TypedResults.Ok(ToResponse(note)) : saved.ToProblem();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        Guid familyId, Guid noteId, NotesDbContext db, HttpRequest httpRequest, CancellationToken cancellationToken)
    {
        var note = await db.Notes.AsTracking().SingleOrDefaultAsync(n => n.FamilyId == familyId && n.Id == noteId, cancellationToken);
        if (note is null)
        {
            return TypedResults.NoContent();
        }

        if (httpRequest.Headers.IfMatch.Count > 0)
        {
            var expected = ETags.FromIfMatch(httpRequest);
            if (expected.IsFailure)
            {
                return expected.ToProblem();
            }

            db.ExpectVersion(note, expected.Value);
        }

        db.Notes.Remove(note);
        var saved = await db.SaveWithConcurrencyCheckAsync(Resource, cancellationToken);
        return saved.IsSuccess ? TypedResults.NoContent() : saved.ToProblem();
    }

    private static NoteResponse ToResponse(Note n) => new(
        n.Id, n.Title, n.Content, n.CreatedByUserId, n.CreatedAt, n.UpdatedByUserId, n.UpdatedAt, ETags.Format(n.Version));
}
