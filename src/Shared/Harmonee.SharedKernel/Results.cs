namespace Harmonee.SharedKernel;

/// <summary>The category of an expected failure. Determines how the failure is surfaced to callers (e.g. HTTP status).</summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,
    PreconditionFailed,
    PreconditionRequired,
}

/// <summary>An expected business failure. Unexpected failures are exceptions, not errors.</summary>
/// <param name="Code">Stable, machine-readable code clients can switch on (e.g. <c>family.last_owner</c>).</param>
/// <param name="Message">Human-readable explanation.</param>
/// <param name="Kind">Category of failure.</param>
/// <param name="Field">Optional request field the failure relates to (validation failures).</param>
public sealed record Error(string Code, string Message, ErrorKind Kind, string? Field = null)
{
    public static Error Validation(string code, string message, string? field = null) => new(code, message, ErrorKind.Validation, field);

    public static Error NotFound(string code, string message) => new(code, message, ErrorKind.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorKind.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorKind.Forbidden);
}

/// <summary>Outcome of an operation that can fail in an expected way.</summary>
public class Result
{
    private static readonly Result s_success = new(null);

    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => Error is not null;

    public static Result Success() => s_success;

    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => new(error);
}

/// <summary>Outcome of an operation that produces a value or fails in an expected way.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null) => _value = value;

    private Result(Error error)
        : base(error)
    {
    }

    /// <summary>The value. Only valid when <see cref="Result.IsSuccess"/> is true.</summary>
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}
