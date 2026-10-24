using System.Net.Http.Headers;
using Harmonee.Web.Authentication;

namespace Harmonee.Web.Api;

/// <summary>Attaches the signed-in user's access token to API calls. Tokens never leave the server.</summary>
public sealed class UserAccessTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = accessor.HttpContext ?? throw new InvalidOperationException("API calls must be made while handling a request.");
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var provider = context.RequestServices.GetRequiredService<IApiAccessTokenProvider>();
            var header = await provider.GetAuthorizationHeaderAsync(context.User, cancellationToken);
            request.Headers.Authorization = AuthenticationHeaderValue.Parse(header);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
