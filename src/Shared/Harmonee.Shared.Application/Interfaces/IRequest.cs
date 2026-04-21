using System;

namespace Harmonee.Shared.Application.Interfaces;

public interface IRequest<IResource>
{
    public static string Route = typeof(IResource).Name;
    public static HttpMethod HttpMethod = HttpMethod.Post;
    public string GetFormattedRoute(object[] args) => string.Format(Route, args);
}
