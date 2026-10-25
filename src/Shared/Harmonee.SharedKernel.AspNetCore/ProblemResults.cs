using Harmonee.Api.Contracts.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Harmonee.SharedKernel.AspNetCore;

/// <summary>Maps expected failures to RFC 9457 Problem Details responses in one place.</summary>
public static class ProblemResults
{
    public static ProblemHttpResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        ProblemDetails problem = error.Kind == ErrorKind.Validation
            ? new HttpValidationProblemDetails(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { [error.Field ?? "request"] = [error.Message] })
            {
                Title = "One or more validation errors occurred.",
                Detail = error.Message,
            }
            : new ProblemDetails { Title = error.Message };

        problem.Status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.PreconditionFailed => StatusCodes.Status412PreconditionFailed,
            ErrorKind.PreconditionRequired => StatusCodes.Status428PreconditionRequired,
            _ => StatusCodes.Status400BadRequest,
        };
        problem.Type = ProblemTypes.For(error.Code);
        problem.Extensions["code"] = error.Code;
        return TypedResults.Problem(problem);
    }

    public static ProblemHttpResult ToProblem(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Error?.ToProblem() ?? throw new InvalidOperationException("Cannot convert a successful result to a problem.");
    }
}
