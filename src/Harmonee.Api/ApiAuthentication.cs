using Harmonee.ServiceDefaults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

namespace Harmonee.Api;

internal static partial class ApiAuthentication
{
    /// <summary>Validates access tokens from Entra External ID, or development tokens minted by the local web app.</summary>
    public static WebApplicationBuilder AddApiAuthentication(this WebApplicationBuilder builder)
    {
        var authentication = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

        // Handler behind policy.RequireScope(...). Registered explicitly so it works for every authentication mode.
        builder.Services.AddRequiredScopeAuthorization();

        if (!builder.UseDevelopmentAuthentication())
        {
            authentication.AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
            return builder;
        }

        var signingKey = builder.Configuration.GetDevelopmentSigningKey();
        authentication.AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = HarmoneeAuthenticationModes.DevelopmentIssuer,
                ValidAudience = HarmoneeAuthenticationModes.DevelopmentAudience,
                IssuerSigningKey = new SymmetricSecurityKey(signingKey),
                NameClaimType = "name",
                RoleClaimType = "roles",
            };
        });

        builder.Services.AddHostedService<DevelopmentAuthenticationWarning>();
        return builder;
    }

    private sealed partial class DevelopmentAuthenticationWarning(ILogger<DevelopmentAuthenticationWarning> logger) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            LogWarning(logger);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        [LoggerMessage(Level = LogLevel.Warning, Message = "DEVELOPMENT AUTHENTICATION IS ENABLED. Tokens are minted locally; never use this outside local development.")]
        private static partial void LogWarning(ILogger logger);
    }
}
