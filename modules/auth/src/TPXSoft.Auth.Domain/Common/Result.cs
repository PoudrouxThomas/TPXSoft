namespace TPXSoft.Auth.Domain.Common;

/// <summary>
/// A minimal success/failure result carrying either a value or an <see cref="AuthError"/>.
/// Deliberately small -- just enough for AuthService's public surface, not a general-purpose
/// functional-result library.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public AuthError Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result is a failure ({Error}); it has no value.");

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
        Error = default;
    }

    private Result(AuthError error)
    {
        _value = default;
        IsSuccess = false;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(AuthError error) => new(error);
}
