using Harmonee.Shared.Application.Interfaces;

namespace Harmonee.Shared.Application.Models;

public abstract record class Request(object? Payload = null) : IRequest
{
    public virtual HttpMethod HttpMethod { get; } = HttpMethod.Get;
    public abstract string RouteTemplate { get; }
    public string GetRoute(object[]? parameters = null)
        => parameters is null || !parameters.Any() ?
            RouteTemplate :
            string.Format(RouteTemplate, parameters);
}