using Harmonee.Web.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Harmonee.Web.Tests;

public sealed class WebClientCredentialsTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void Managed_identity_credential_defaults_to_the_containers_identity()
    {
        var configuration = Configuration(
            ("AzureAd:ClientCredentials:0:SourceType", "SignedAssertionFromManagedIdentity"),
            ("AZURE_CLIENT_ID", "11111111-2222-3333-4444-555555555555"));

        var resolved = WebClientCredentials.Resolve(configuration, new TestEnvironment(Environments.Production));

        resolved["AzureAd:ClientCredentials:0:ManagedIdentityClientId"].ShouldBe("11111111-2222-3333-4444-555555555555");
    }

    [Fact]
    public void Explicit_managed_identity_client_id_is_kept()
    {
        var configuration = Configuration(
            ("AzureAd:ClientCredentials:0:SourceType", "SignedAssertionFromManagedIdentity"),
            ("AzureAd:ClientCredentials:0:ManagedIdentityClientId", "explicit"),
            ("AZURE_CLIENT_ID", "other"));

        WebClientCredentials.Resolve(configuration, new TestEnvironment(Environments.Production)).ShouldBeEmpty();
    }

    [Fact]
    public void Client_secret_is_refused_outside_development()
    {
        var configuration = Configuration(
            ("AzureAd:ClientCredentials:0:SourceType", "ClientSecret"),
            ("AzureAd:ClientCredentials:0:ClientSecret", "not-a-real-secret"));

        Should.Throw<InvalidOperationException>(() => WebClientCredentials.Resolve(configuration, new TestEnvironment(Environments.Production)))
            .Message.ShouldContain("only allowed in the Development environment");
    }

    [Fact]
    public void Client_secret_is_allowed_in_development()
    {
        var configuration = Configuration(
            ("AzureAd:ClientCredentials:0:SourceType", "ClientSecret"),
            ("AzureAd:ClientCredentials:0:ClientSecret", "not-a-real-secret"));

        WebClientCredentials.Resolve(configuration, new TestEnvironment(Environments.Development)).ShouldBeEmpty();
    }

    [Fact]
    public void Missing_credentials_fail_fast()
    {
        Should.Throw<InvalidOperationException>(() => WebClientCredentials.Resolve(Configuration(), new TestEnvironment(Environments.Production)));
    }

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;

        public string ApplicationName { get; set; } = "Harmonee.Web";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
