using Harmonee.AuthService.Data;
//using Constants = Harmonee.Resources.Constants;
using Harmonee.Core.Models.Auth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

//builder.AddSqlServerDbContext<HarmoneeAuthContext>(Constants.Services.Auth.DatabaseName);

// Add services to the container.
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery();
builder.Services.AddIdentityApiEndpoints<HarmoneeUser>()
    .AddEntityFrameworkStores<HarmoneeAuthContext>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapIdentityApi<HarmoneeUser>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

var scopeRequiredByApi = app.Configuration["AzureAd:Scopes"] ?? "";

app.Run();
