namespace Harmonee.Web.Security;

/// <summary>Security headers for HTML responses, including a strict Content Security Policy (no inline scripts or styles).</summary>
internal static class SecurityHeaders
{
    public static IApplicationBuilder UseWebSecurityHeaders(this WebApplication app)
    {
        // Sign-out redirects to the identity provider after a form post, so form-action must allow it.
        var identityProvider = app.Configuration["AzureAd:Instance"]?.TrimEnd('/');
        var formAction = string.IsNullOrWhiteSpace(identityProvider) || identityProvider.Contains('<', StringComparison.Ordinal)
            ? "'self'"
            : $"'self' {identityProvider}";

        // Development tooling (hot reload / browser refresh) uses a local websocket.
        var connect = app.Environment.IsDevelopment() ? "'self' ws: wss:" : "'self'";

        var csp = string.Join("; ",
            "default-src 'self'",
            "script-src 'self'",
            "style-src 'self'",
            "img-src 'self' data:",
            "font-src 'self'",
            $"connect-src {connect}",
            $"form-action {formAction}",
            "frame-ancestors 'none'",
            "base-uri 'self'",
            "object-src 'none'");

        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = csp;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            await next(context);
        });
    }
}
