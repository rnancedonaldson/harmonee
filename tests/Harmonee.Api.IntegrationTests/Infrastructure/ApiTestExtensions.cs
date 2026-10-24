using System.Net;
using System.Net.Http.Json;
using Harmonee.Api.Contracts.Common;
using Harmonee.Api.Contracts.Families;
using Microsoft.AspNetCore.Mvc;

namespace Harmonee.Api.IntegrationTests.Infrastructure;

internal static class ApiTestExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new ShouldAssertException($"Expected {(int)expected} but got {(int)response.StatusCode}: {body}");
        }

        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    public static async Task<ProblemDetails> ReadProblemAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
    }

    public static string? Code(this ProblemDetails problem) =>
        problem.Extensions.TryGetValue("code", out var code) ? code?.ToString() : null;

    public static async Task<FamilyResponse> CreateFamilyAsync(this HttpClient client, string name = "The Testers") =>
        await (await client.PostAsJsonAsync("/v1/families", new CreateFamilyRequest(name))).ReadAsync<FamilyResponse>(HttpStatusCode.Created);

    /// <summary>Creates a family owned by <paramref name="owner"/> and joins <paramref name="member"/> with the default (Viewer) role.</summary>
    public static async Task<(FamilyResponse Family, Guid MemberId)> CreateFamilyWithMemberAsync(HttpClient owner, HttpClient member)
    {
        var family = await owner.CreateFamilyAsync();
        var joined = await (await member.PostAsJsonAsync("/v1/families/join", new JoinFamilyRequest(family.InviteCode!)))
            .ReadAsync<FamilyMembershipSummary>(HttpStatusCode.Created);
        joined.FamilyId.ShouldBe(family.Id);

        var refreshed = await (await owner.GetAsync($"/v1/families/{family.Id}")).ReadAsync<FamilyResponse>();
        var memberId = refreshed.Members.Single(m => !m.IsOwner).MemberId;
        return (refreshed, memberId);
    }

    public static Guid RoleId(this FamilyResponse family, string name) => family.Roles.Single(r => string.Equals(r.Name, name, StringComparison.Ordinal)).Id;

    public static HttpRequestMessage WithIfMatch(this HttpRequestMessage request, string etag)
    {
        request.Headers.TryAddWithoutValidation("If-Match", etag);
        return request;
    }

    public static HttpRequestMessage Put<T>(string url, T body) => new(HttpMethod.Put, url) { Content = JsonContent.Create(body) };

    public static IReadOnlyList<FamilyPermission> AllPermissions => FamilyPermissions.All;
}
