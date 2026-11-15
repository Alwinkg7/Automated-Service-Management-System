namespace ServiceApp.Core.Common;

/// <summary>Outcome of a business operation that returns a value.</summary>
public class Result<T>
{
    public bool IsSuccess { get; private init; }
    public T? Data { get; private init; }
    public string? ErrorMessage { get; private init; }

    /// <summary>
    /// Error classification. <see cref="Failure(string)"/> defaults to
    /// <see cref="ErrorType.Validation"/> so existing callers keep mapping to 400.
    /// </summary>
    public ErrorType ErrorType { get; private init; }

    private Result() { }

    public static Result<T> Success(T data) =>
        new() { IsSuccess = true, Data = data, ErrorType = ErrorType.None };

    public static Result<T> Failure(string errorMessage) =>
        Failure(errorMessage, ErrorType.Validation);

    public static Result<T> Failure(string errorMessage, ErrorType errorType) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage, ErrorType = errorType };
}

/// <summary>Outcome of a business operation without a return value.</summary>
public class Result
{
    public bool IsSuccess { get; private init; }
    public string? ErrorMessage { get; private init; }

    /// <inheritdoc cref="Result{T}.ErrorType"/>
    public ErrorType ErrorType { get; private init; }

    private Result() { }

    public static Result Success() =>
        new() { IsSuccess = true, ErrorType = ErrorType.None };

    public static Result Failure(string errorMessage) =>
        Failure(errorMessage, ErrorType.Validation);

    public static Result Failure(string errorMessage, ErrorType errorType) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage, ErrorType = errorType };
}
