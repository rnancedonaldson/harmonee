using System;
using Harmonee.Shared.Domain.Interfaces;

namespace Harmonee.Shared.Application.Interfaces;

public interface ICommand<TResource, TResult> where TResource : IResource where TResult : IResult
{
    public static string Route = typeof(TResource).Name;
    public static HttpMethod HttpMethod = HttpMethod.Post;
    public string GetFormattedRoute();
}
