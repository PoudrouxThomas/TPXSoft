namespace TPXSoft.Documents.Domain.Common;

/// <summary>
/// A minimal success/failure result carrying either a value or a <see cref="DocumentError"/>.
/// Deliberately small -- just enough for the Domain services' public surface, not a
/// general-purpose functional-result library. Mirrors TPXSoft.Auth.Domain.Common.Result{T}.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public DocumentError Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result is a failure ({Error}); it has no value.");

    private Result(T value)
    {
        _value = value;
        IsSuccess = true;
        Error = default;
    }

    private Result(DocumentError error)
    {
        _value = default;
        IsSuccess = false;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(DocumentError error) => new(error);
}

/// <summary>
/// Non-generic counterpart of <see cref="Result{T}"/> for operations that either succeed with no
/// value (e.g. delete) or fail with a <see cref="DocumentError"/>.
/// </summary>
public readonly struct Result
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public DocumentError Error { get; }

    private Result(bool isSuccess, DocumentError error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, default);

    public static Result Failure(DocumentError error) => new(false, error);
}
