namespace backend.Common;

/// <summary>
/// Represents the result of an operation that can succeed or fail
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
public record Result<T>
{
    public bool IsSuccess { get; init; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; init; }
    public string? Error { get; init; }
    public Exception? Exception { get; init; }

    private Result() { }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static Result<T> Success(T value) => new()
    {
        IsSuccess = true,
        Value = value
    };

    /// <summary>
    /// Creates a failed result with an error message
    /// </summary>
    public static Result<T> Failure(string error) => new()
    {
        IsSuccess = false,
        Error = error
    };

    /// <summary>
    /// Creates a failed result with an error message and exception
    /// </summary>
    public static Result<T> Failure(string error, Exception exception) => new()
    {
        IsSuccess = false,
        Error = error,
        Exception = exception
    };

    /// <summary>
    /// Matches the result to different handlers based on success/failure
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<string, TResult> onFailure)
    {
        return IsSuccess && Value != null
            ? onSuccess(Value)
            : onFailure(Error ?? "Unknown error");
    }
}

/// <summary>
/// Represents the result of an operation without a return value
/// </summary>
public record Result
{
    public bool IsSuccess { get; init; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; init; }
    public Exception? Exception { get; init; }

    private Result() { }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static Result Success() => new()
    {
        IsSuccess = true
    };

    /// <summary>
    /// Creates a failed result with an error message
    /// </summary>
    public static Result Failure(string error) => new()
    {
        IsSuccess = false,
        Error = error
    };

    /// <summary>
    /// Creates a failed result with an error message and exception
    /// </summary>
    public static Result Failure(string error, Exception exception) => new()
    {
        IsSuccess = false,
        Error = error,
        Exception = exception
    };
}
