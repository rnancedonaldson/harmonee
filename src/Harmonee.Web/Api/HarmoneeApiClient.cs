using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Harmonee.Api.Contracts.Calendar;
using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Harmonee.Api.Contracts.Kitchen;
using Harmonee.Api.Contracts.Notes;
using Microsoft.AspNetCore.Mvc;

namespace Harmonee.Web.Api;

/// <summary>Typed client for the Harmonee API, built on the shared request/response contracts.</summary>
public sealed class HarmoneeApiClient(HttpClient http)
{
    // Me & families
    public Task<ApiResult<MeResponse>> GetMeAsync(CancellationToken ct) => GetAsync<MeResponse>("v1/me", ct);

    public Task<ApiResult<FamilyResponse>> CreateFamilyAsync(CreateFamilyRequest request, CancellationToken ct) =>
        SendAsync<FamilyResponse>(HttpMethod.Post, "v1/families", request, null, ct);

    public Task<ApiResult<FamilyMembershipSummary>> JoinFamilyAsync(JoinFamilyRequest request, CancellationToken ct) =>
        SendAsync<FamilyMembershipSummary>(HttpMethod.Post, "v1/families/join", request, null, ct);

    public Task<ApiResult<FamilyResponse>> GetFamilyAsync(Guid familyId, CancellationToken ct) =>
        GetAsync<FamilyResponse>($"v1/families/{familyId}", ct);

    public Task<ApiResult<FamilyResponse>> UpdateFamilyAsync(Guid familyId, UpdateFamilyRequest request, CancellationToken ct) =>
        SendAsync<FamilyResponse>(HttpMethod.Put, $"v1/families/{familyId}", request, null, ct);

    public Task<ApiResult<InviteCodeResponse>> RegenerateInviteCodeAsync(Guid familyId, CancellationToken ct) =>
        SendAsync<InviteCodeResponse>(HttpMethod.Post, $"v1/families/{familyId}/invite-code", null, null, ct);

    public Task<ApiResult<FamilyResponse>> UpdateMemberAsync(Guid familyId, Guid memberId, UpdateMemberRequest request, CancellationToken ct) =>
        SendAsync<FamilyResponse>(HttpMethod.Put, $"v1/families/{familyId}/members/{memberId}", request, null, ct);

    public Task<ApiOutcome> RemoveMemberAsync(Guid familyId, Guid memberId, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"v1/families/{familyId}/members/{memberId}", null, null, ct);

    public Task<ApiResult<FamilyRoleResponse>> CreateRoleAsync(Guid familyId, SaveRoleRequest request, CancellationToken ct) =>
        SendAsync<FamilyRoleResponse>(HttpMethod.Post, $"v1/families/{familyId}/roles", request, null, ct);

    public Task<ApiOutcome> UpdateRoleAsync(Guid familyId, Guid roleId, SaveRoleRequest request, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, $"v1/families/{familyId}/roles/{roleId}", request, null, ct);

    public Task<ApiOutcome> DeleteRoleAsync(Guid familyId, Guid roleId, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"v1/families/{familyId}/roles/{roleId}", null, null, ct);

    public Task<ApiResult<CursorPage<AdminFamilySummary>>> AdminListFamiliesAsync(string? search, string? cursor, CancellationToken ct) =>
        GetAsync<CursorPage<AdminFamilySummary>>($"v1/admin/families{Query(("search", search), ("cursor", cursor))}", ct);

    // Calendar
    public Task<ApiResult<CalendarEventsResponse>> ListEventsAsync(Guid familyId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        GetAsync<CalendarEventsResponse>($"v1/families/{familyId}/events{Query(("from", from.ToString("O", CultureInfo.InvariantCulture)), ("to", to.ToString("O", CultureInfo.InvariantCulture)))}", ct);

    public Task<ApiResult<CalendarEventResponse>> GetEventAsync(Guid familyId, Guid eventId, CancellationToken ct) =>
        GetAsync<CalendarEventResponse>($"v1/families/{familyId}/events/{eventId}", ct);

    public Task<ApiResult<CalendarEventResponse>> CreateEventAsync(Guid familyId, SaveCalendarEventRequest request, CancellationToken ct) =>
        SendAsync<CalendarEventResponse>(HttpMethod.Post, $"v1/families/{familyId}/events", request, null, ct);

    public Task<ApiResult<CalendarEventResponse>> UpdateEventAsync(Guid familyId, Guid eventId, SaveCalendarEventRequest request, string etag, CancellationToken ct) =>
        SendAsync<CalendarEventResponse>(HttpMethod.Put, $"v1/families/{familyId}/events/{eventId}", request, etag, ct);

    public Task<ApiOutcome> DeleteEventAsync(Guid familyId, Guid eventId, string? etag, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"v1/families/{familyId}/events/{eventId}", null, etag, ct);

    // Shopping lists
    public Task<ApiResult<CursorPage<ShoppingListSummary>>> ListShoppingListsAsync(Guid familyId, string? cursor, CancellationToken ct) =>
        GetAsync<CursorPage<ShoppingListSummary>>($"v1/families/{familyId}/shopping-lists{Query(("cursor", cursor))}", ct);

    public Task<ApiResult<ShoppingListResponse>> GetShoppingListAsync(Guid familyId, Guid listId, CancellationToken ct) =>
        GetAsync<ShoppingListResponse>($"v1/families/{familyId}/shopping-lists/{listId}", ct);

    public Task<ApiResult<ShoppingListResponse>> CreateShoppingListAsync(Guid familyId, SaveShoppingListRequest request, CancellationToken ct) =>
        SendAsync<ShoppingListResponse>(HttpMethod.Post, $"v1/families/{familyId}/shopping-lists", request, null, ct);

    public Task<ApiOutcome> RenameShoppingListAsync(Guid familyId, Guid listId, SaveShoppingListRequest request, string etag, CancellationToken ct) =>
        SendAsync(HttpMethod.Put, $"v1/families/{familyId}/shopping-lists/{listId}", request, etag, ct);

    public Task<ApiOutcome> DeleteShoppingListAsync(Guid familyId, Guid listId, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"v1/families/{familyId}/shopping-lists/{listId}", null, null, ct);

    public Task<ApiResult<ShoppingListItemResponse>> AddShoppingListItemAsync(Guid familyId, Guid listId, SaveShoppingListItemRequest request, CancellationToken ct) =>
        SendAsync<ShoppingListItemResponse>(HttpMethod.Post, $"v1/families/{familyId}/shopping-lists/{listId}/items", request, null, ct);

    public Task<ApiResult<ShoppingListItemResponse>> UpdateShoppingListItemAsync(
        Guid familyId, Guid listId, Guid itemId, SaveShoppingListItemRequest request, string etag, CancellationToken ct) =>
        SendAsync<ShoppingListItemResponse>(HttpMethod.Put, $"v1/families/{familyId}/shopping-lists/{listId}/items/{itemId}", request, etag, ct);

    public Task<ApiOutcome> DeleteShoppingListItemAsync(Guid familyId, Guid listId, Guid itemId, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"v1/families/{familyId}/shopping-lists/{listId}/items/{itemId}", null, null, ct);

    // Meal plans
    public Task<ApiResult<MealPlanResponse>> GetMealPlanAsync(Guid familyId, DateOnly from, DateOnly to, CancellationToken ct) =>
        GetAsync<MealPlanResponse>($"v1/families/{familyId}/meal-plan{Query(("from", from.ToString("O", CultureInfo.InvariantCulture)), ("to", to.ToString("O", CultureInfo.InvariantCulture)))}", ct);

    public Task<ApiResult<MealPlanEntryResponse>> GetMealPlanEntryAsync(Guid familyId, Guid entryId, CancellationToken ct) =>
        GetAsync<MealPlanEntryResponse>($"v1/families/{familyId}/meal-plan/entries/{entryId}", ct);

    public Task<ApiResult<MealPlanEntryResponse>> CreateMealPlanEntryAsync(Guid familyId, SaveMealPlanEntryRequest request, CancellationToken ct) =>
        SendAsync<MealPlanEntryResponse>(HttpMethod.Post, $"v1/families/{familyId}/meal-plan/entries", request, null, ct);

    public Task<ApiResult<MealPlanEntryResponse>> UpdateMealPlanEntryAsync(Guid familyId, Guid entryId, SaveMealPlanEntryRequest request, string etag, CancellationToken ct) =>
        SendAsync<MealPlanEntryResponse>(HttpMethod.Put, $"v1/families/{familyId}/meal-plan/entries/{entryId}", request, etag, ct);

    public Task<ApiOutcome> DeleteMealPlanEntryAsync(Guid familyId, Guid entryId, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"v1/families/{familyId}/meal-plan/entries/{entryId}", null, null, ct);

    // Notes
    public Task<ApiResult<CursorPage<NoteSummary>>> ListNotesAsync(Guid familyId, string? cursor, int? limit, CancellationToken ct) =>
        GetAsync<CursorPage<NoteSummary>>($"v1/families/{familyId}/notes{Query(("cursor", cursor), ("limit", limit?.ToString(CultureInfo.InvariantCulture)))}", ct);

    public Task<ApiResult<NoteResponse>> GetNoteAsync(Guid familyId, Guid noteId, CancellationToken ct) =>
        GetAsync<NoteResponse>($"v1/families/{familyId}/notes/{noteId}", ct);

    public Task<ApiResult<NoteResponse>> CreateNoteAsync(Guid familyId, SaveNoteRequest request, CancellationToken ct) =>
        SendAsync<NoteResponse>(HttpMethod.Post, $"v1/families/{familyId}/notes", request, null, ct);

    public Task<ApiResult<NoteResponse>> UpdateNoteAsync(Guid familyId, Guid noteId, SaveNoteRequest request, string etag, CancellationToken ct) =>
        SendAsync<NoteResponse>(HttpMethod.Put, $"v1/families/{familyId}/notes/{noteId}", request, etag, ct);

    public Task<ApiOutcome> DeleteNoteAsync(Guid familyId, Guid noteId, CancellationToken ct) =>
        SendAsync(HttpMethod.Delete, $"v1/families/{familyId}/notes/{noteId}", null, null, ct);

    // Plumbing
    private async Task<ApiResult<T>> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await http.GetAsync(new Uri(url, UriKind.Relative), ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body, string? etag, CancellationToken ct)
    {
        using var request = Build(method, url, body, etag);
        using var response = await http.SendAsync(request, ct);
        return await ReadAsync<T>(response, ct);
    }

    private async Task<ApiOutcome> SendAsync(HttpMethod method, string url, object? body, string? etag, CancellationToken ct)
    {
        using var request = Build(method, url, body, etag);
        using var response = await http.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? ApiOutcome.Ok : new ApiOutcome(await ReadProblemAsync(response, ct));
    }

    private static HttpRequestMessage Build(HttpMethod method, string url, object? body, string? etag)
    {
        var request = new HttpRequestMessage(method, new Uri(url, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        if (!string.IsNullOrEmpty(etag))
        {
            request.Headers.TryAddWithoutValidation("If-Match", etag);
        }

        return request;
    }

    private static async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            return ApiResult<T>.Failure(await ReadProblemAsync(response, ct));
        }

        var value = await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new InvalidOperationException($"The API returned an empty {typeof(T).Name}.");
        return ApiResult<T>.Success(value);
    }

    private static async Task<ApiProblem> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        HttpValidationProblemDetails? problem = null;
        if (response.Content.Headers.ContentType?.MediaType is "application/problem+json")
        {
            problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(ct);
        }

        var fallback = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again.",
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            HttpStatusCode.NotFound => "That item doesn't exist or you don't have access to it.",
            HttpStatusCode.TooManyRequests => "You're doing that too often. Please wait a moment and try again.",
            _ => "Something went wrong. Please try again.",
        };

        return new ApiProblem(
            response.StatusCode,
            problem?.Title ?? fallback,
            problem?.Extensions.TryGetValue("code", out var code) == true ? code?.ToString() : null,
            problem?.Errors.AsReadOnly() ?? new Dictionary<string, string[]>(StringComparer.Ordinal).AsReadOnly(),
            problem?.Extensions.TryGetValue("traceId", out var traceId) == true ? traceId?.ToString() : null);
    }

    private static string Query(params (string Name, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value!)}")
            .ToList();
        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }
}
