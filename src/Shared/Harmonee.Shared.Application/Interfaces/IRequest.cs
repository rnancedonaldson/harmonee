namespace Harmonee.Shared.Application.Interfaces;

public interface IRequest
{
    public static HttpMethod HttpMethod { get; }
    public static string RouteTemplate { get; }
    public string GetRoute(object[]? parameters = null);
}
