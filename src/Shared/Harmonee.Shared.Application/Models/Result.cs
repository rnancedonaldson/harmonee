using Harmonee.Shared.Application.Interfaces;

namespace Harmonee.Shared.Application.Models;

public record Result(string? ErrorMessage = null) : IResult
{
    public bool IsSuccess => ErrorMessage is null;

    public static implicit operator bool(Result result) => result.IsSuccess;
    public static implicit operator string(Result result) => result.ErrorMessage ?? string.Empty;
    public static implicit operator Result(string errorMessage) => new(errorMessage);
    public static implicit operator Result(bool isSuccess) => isSuccess ? Success() : new("An error occurred.");

    public static Result Success() => new();
}

public record Result<T>(T? Value = default, string? ErrorMessage = null) : IResult
{
    public bool IsSuccess => Value is not null && ErrorMessage is null;

    public static implicit operator bool(Result<T> result) => result.IsSuccess;
    public static implicit operator string(Result<T> result) => result.ErrorMessage ?? string.Empty;
    public static implicit operator Result<T>(string errorMessage) => new(default, errorMessage);
    public static implicit operator Result<T>(T? value) => value is not null ? new(value) : new(default, "An error occurred.");
}