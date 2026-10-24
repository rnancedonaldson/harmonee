using System.Net;

namespace Harmonee.Web.Api;

/// <summary>A Problem Details response from the API, reduced to what the UI needs.</summary>
public sealed record ApiProblem(HttpStatusCode Status, string Title, string? Code, IReadOnlyDictionary<string, string[]> Errors, string? TraceId)
{
    public bool IsNotFound => Status == HttpStatusCode.NotFound;

    public bool IsConcurrencyConflict => Status == HttpStatusCode.PreconditionFailed;

    /// <summary>A message safe and useful to show to the user.</summary>
    public string Message
    {
        get
        {
            if (Errors.Count > 0)
            {
                return string.Join(" ", Errors.Values.SelectMany(v => v));
            }

            return (int)Status >= 500 && TraceId is not null
                ? $"Something went wrong. If it keeps happening, quote reference {TraceId}."
                : Title;
        }
    }
}

/// <summary>Either a value or a problem. Pages render the problem instead of throwing.</summary>
public sealed class ApiResult<T>
{
    private ApiResult(T? value, ApiProblem? problem)
    {
        Value = value;
        Problem = problem;
    }

    public T? Value { get; }

    public ApiProblem? Problem { get; }

    public bool IsSuccess => Problem is null;

    public static ApiResult<T> Success(T value) => new(value, null);

    public static ApiResult<T> Failure(ApiProblem problem) => new(default, problem);
}

/// <summary>Result of an API call with no response body.</summary>
public sealed record ApiOutcome(ApiProblem? Problem)
{
    public static readonly ApiOutcome Ok = new((ApiProblem?)null);

    public bool IsSuccess => Problem is null;
}
