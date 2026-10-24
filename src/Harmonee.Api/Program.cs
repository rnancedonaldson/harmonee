using System.Threading.RateLimiting;
using Asp.Versioning;
using Harmonee.Api;
using Harmonee.Calendar;
using Harmonee.Families;
using Harmonee.Kitchen;
using Harmonee.Notes;
using Harmonee.SharedKernel.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

// Modules (each owns its schema in the shared database)
builder.AddFamiliesModule()
    .AddCalendarModule()
    .AddKitchenModule()
    .AddNotesModule();

// Authentication: access tokens issued by Microsoft Entra External ID for this API.
builder.AddApiAuthentication();

// Authorization: deny by default. Every endpoint needs a signed-in user with the API scope,
// unless it explicitly opts out with AllowAnonymous().
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireScope(HarmoneeAuthorization.AccessScope)
        .Build())
    .AddPolicy(HarmoneeAuthorization.PlatformAdminPolicy, policy => policy
        .RequireAuthenticatedUser()
        .RequireScope(HarmoneeAuthorization.AccessScope)
        .RequireRole(HarmoneeAuthorization.PlatformAdminRole));

// Rate limiting per user (or per IP before authentication).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetTokenBucketLimiter(
            context.User.FindFirst("oid")?.Value
                ?? context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous",
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                TokensPerPeriod = 50,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                QueueLimit = 0,
            }));
});

// API plumbing
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddValidation();
builder.Services.AddRequestTimeouts(options => options.DefaultPolicy = new() { Timeout = TimeSpan.FromSeconds(30) });
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1);
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'V";
        options.SubstituteApiVersionInUrl = true;
    })
    .AddOpenApi(options => options.Document.AddDocumentTransformer<HarmoneeOpenApiTransformer>());

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSecurityHeaders();
// No HTTPS redirection: API clients shouldn't follow redirects to fix their scheme. TLS is enforced at the
// edge (Front Door / Container Apps ingress with allowInsecure=false).
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseRequestTimeouts();

app.MapDefaultEndpoints();
app.MapOpenApi().WithDocumentPerVersion().AllowAnonymous();
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference().AllowAnonymous();
}

var v1 = app.NewVersionedApi("Harmonee")
    .MapGroup("/v{version:apiVersion}")
    .HasApiVersion(1.0)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .ProducesProblem(StatusCodes.Status500InternalServerError);

v1.MapFamiliesEndpoints();
v1.MapCalendarEndpoints();
v1.MapKitchenEndpoints();
v1.MapNotesEndpoints();

await app.RunAsync();

/// <summary>Entry point; partial so integration tests can reference it with WebApplicationFactory.</summary>
public partial class Program;
