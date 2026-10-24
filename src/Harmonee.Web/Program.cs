using Azure.Identity;
using Harmonee.Web.Api;
using Harmonee.Web.Authentication;
using Harmonee.Web.Components;
using Harmonee.Web.Security;
using Harmonee.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

// Blazor with static server-side rendering only: every API call happens on the server, inside a request,
// with the user's token from the server-side cache. No WebAssembly, no circuits, no tokens in the browser.
builder.Services.AddRazorComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

builder.AddWebAuthentication();
builder.Services.AddAuthorization();

// Cookies and antiforgery tokens must survive restarts and work across instances in Azure.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Harmonee.Web");
if (builder.Configuration["DataProtection:BlobUri"] is { Length: > 0 } blobUri
    && builder.Configuration["DataProtection:KeyVaultKeyUri"] is { Length: > 0 } keyUri)
{
    var credential = new DefaultAzureCredential();
    dataProtection
        .PersistKeysToAzureBlobStorage(new Uri(blobUri), credential)
        .ProtectKeysWithAzureKeyVault(new Uri(keyUri), credential);
}

// Typed API client: service discovery + standard resilience (from ServiceDefaults) + user token.
builder.Services.AddTransient<UserAccessTokenHandler>();
builder.Services.AddHttpClient<HarmoneeApiClient>((sp, http) =>
        http.BaseAddress = sp.GetRequiredService<IOptions<HarmoneeApiOptions>>().Value.BaseAddress)
    .AddHttpMessageHandler<UserAccessTokenHandler>();
builder.Services.AddScoped<UserSession>();
builder.Services.AddScoped<UserTimeZone>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseWebSecurityHeaders();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapDefaultEndpoints();
app.MapAuthenticationEndpoints();
app.MapRazorComponents<App>();

await app.RunAsync();
