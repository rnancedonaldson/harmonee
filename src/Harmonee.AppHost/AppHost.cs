using System.Security.Cryptography;
using Harmonee.SharedKernel;

var builder = DistributedApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Database: one Azure SQL database; each module owns its own schema.
// Locally, set ConnectionStrings:harmonee-db (e.g. SQL Server LocalDB) to skip containers,
// otherwise a SQL Server container is used. In Azure, Azure SQL is provisioned.
// ---------------------------------------------------------------------------
IResourceBuilder<IResourceWithConnectionString> database;
if (builder.ExecutionContext.IsRunMode && !string.IsNullOrWhiteSpace(builder.Configuration[$"ConnectionStrings:{ResourceNames.Database}"]))
{
    database = builder.AddConnectionString(ResourceNames.Database);
}
else
{
    database = builder.AddAzureSqlServer("sql")
        .RunAsContainer(container => container.WithLifetime(ContainerLifetime.Persistent))
        .AddDatabase(ResourceNames.Database);
}

var migrations = builder.AddProject<Projects.Harmonee_MigrationService>(ResourceNames.Migrations)
    .WithReference(database);

var api = builder.AddProject<Projects.Harmonee_Api>(ResourceNames.Api)
    .WithReference(database)
    .WaitForCompletion(migrations);

var web = builder.AddProject<Projects.Harmonee_Web>(ResourceNames.Web)
    .WithReference(api)
    .WaitFor(api)
    .WithExternalHttpEndpoints();

// Optional local override, e.g. "http://api" when the ASP.NET Core dev certificate isn't trusted on this machine.
if (builder.ExecutionContext.IsRunMode && builder.Configuration["Harmonee:ApiBaseAddress"] is { Length: > 0 } apiBaseAddress)
{
    web.WithEnvironment("HarmoneeApi__BaseAddress", apiBaseAddress);
}

// ---------------------------------------------------------------------------
// Authentication. "Development" works out of the box (local only). "EntraExternalId" uses your
// Microsoft Entra External ID tenant; set the parameters below in this project's user secrets.
// See docs/getting-started.md.
// ---------------------------------------------------------------------------
var mode = builder.Configuration["Harmonee:AuthenticationMode"] ?? "EntraExternalId";
api.WithEnvironment("Authentication__Mode", mode);
web.WithEnvironment("Authentication__Mode", mode);

if (string.Equals(mode, "Development", StringComparison.OrdinalIgnoreCase))
{
    // A fresh signing key per run: development tokens can't outlive the session that issued them.
    var signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    api.WithEnvironment("Authentication__DevelopmentSigningKey", signingKey);
    web.WithEnvironment("Authentication__DevelopmentSigningKey", signingKey);
}
else
{
    var instance = builder.AddParameter("entra-instance");          // https://<tenant-subdomain>.ciamlogin.com/
    var tenantId = builder.AddParameter("entra-tenant-id");
    var apiClientId = builder.AddParameter("api-client-id");
    var webClientId = builder.AddParameter("web-client-id");
    var webClientSecret = builder.AddParameter("web-client-secret", secret: true);

    api.WithEnvironment("AzureAd__Instance", instance)
        .WithEnvironment("AzureAd__TenantId", tenantId)
        .WithEnvironment("AzureAd__ClientId", apiClientId);

    web.WithEnvironment("AzureAd__Instance", instance)
        .WithEnvironment("AzureAd__TenantId", tenantId)
        .WithEnvironment("AzureAd__ClientId", webClientId)
        .WithEnvironment("AzureAd__ClientCredentials__0__SourceType", "ClientSecret")
        .WithEnvironment("AzureAd__ClientCredentials__0__ClientSecret", webClientSecret)
        .WithEnvironment("HarmoneeApi__Scopes__0", ReferenceExpression.Create($"api://{apiClientId}/Harmonee.Access"));
}

await builder.Build().RunAsync();
