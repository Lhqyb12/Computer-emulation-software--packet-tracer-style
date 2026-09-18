namespace NetSim.Application.Common;

/// <summary>
/// Outcome of an application service operation that has no return value. Used for
/// expected, "normal" failures (not found, invalid state, ...) so that callers are not
/// forced to use exceptions for routine user mistakes. Genuine domain-rule violations
/// still surface as <see cref="Core.Common.Exceptions.DomainException"/> ג€” this type is
/// deliberately not used to wrap those.
/// </summary>
public sealed class OperationResult
{
    private OperationResult(bool isSuccess, OperationErrorType? errorType, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public OperationErrorType? ErrorType { get; }

    public string? ErrorMessage { get; }

    public static OperationResult Success() => new(true, null, null);

    public static OperationResult Failure(OperationErrorType errorType, string errorMessage) =>
        new(false, errorType, errorMessage);
}

/// <summary>
/// Outcome of an application service operation that returns a value on success. See
/// <see cref="OperationResult"/> for the rationale.
/// </summary>
public sealed class OperationResult<T>
{
    private OperationResult(bool isSuccess, T? value, OperationErrorType? errorType, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    public OperationErrorType? ErrorType { get; }

    public string? ErrorMessage { get; }

    public static OperationResult<T> Success(T value) => new(true, value, null, null);

    public static OperationResult<T> Failure(OperationErrorType errorType, string errorMessage) =>
        new(false, default, errorType, errorMessage);
}
