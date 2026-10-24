using System.Net;
using System.Runtime.CompilerServices;
using Harmonee.Api.IntegrationTests.Infrastructure;

namespace Harmonee.Api.IntegrationTests;

/// <summary>
/// Guards the public API contract. Any change to the OpenAPI document fails this test until the committed
/// snapshot is updated, so contract changes are always visible (and reviewed) in pull requests.
/// To accept a change, run the tests with HARMONEE_UPDATE_SNAPSHOTS=1 and commit the updated snapshot.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class OpenApiContractTests(HarmoneeApiFactory factory)
{
    [Fact]
    public async Task OpenApi_document_matches_the_committed_snapshot()
    {
        var response = await factory.CreateClient().GetAsync("/openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var actual = Normalize(await response.Content.ReadAsStringAsync());

        var snapshotPath = SnapshotPath();
        if (string.Equals(Environment.GetEnvironmentVariable("HARMONEE_UPDATE_SNAPSHOTS"), "1", StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(snapshotPath, actual);
        }

        File.Exists(snapshotPath).ShouldBeTrue($"Snapshot missing. Run with HARMONEE_UPDATE_SNAPSHOTS=1 to create {snapshotPath}.");
        var expected = Normalize(await File.ReadAllTextAsync(snapshotPath));
        actual.ShouldBe(expected, "The OpenAPI contract changed. If intended, re-run with HARMONEE_UPDATE_SNAPSHOTS=1 and commit the snapshot.");
    }

    private static string Normalize(string json) => json.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd() + "\n";

    private static string SnapshotPath([CallerFilePath] string sourceFile = "") =>
        Path.Combine(Path.GetDirectoryName(sourceFile)!, "Snapshots", "openapi.v1.json");
}
