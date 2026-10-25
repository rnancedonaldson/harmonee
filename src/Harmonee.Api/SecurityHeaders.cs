namespace Harmonee.Api;

internal static class SecurityHeaders
{
    /// <summary>Baseline headers for a JSON API. The edge (Front Door) adds HSTS in Azure; HTTPS redirection covers local.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers.CacheControl = "no-store";
            await next(context);
        });
}
