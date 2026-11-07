namespace NetSim.Application.Common;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The answer of an action that returns nothing on success (e.g. delete, save).
// Expected failures ("not found", "no project open") come back as this answer instead of an
// exception, so the caller sees from the return type that it may fail, and checks IsSuccess
public sealed class OperationResult
{
    // Purpose: Builds an answer. Private, so the only way to make one is Success() or Failure() below.
    // Input:   isSuccess; errorType and errorMessage (null on success).
    // Output:  none (a constructor).
    private OperationResult(bool isSuccess, OperationErrorType? errorType, string? errorMessage)
    {
        IsSuccess = isSuccess;          // did it work?
        ErrorType = errorType;          // what kind of problem (null on success)
        ErrorMessage = errorMessage;    // the text to show (null on success)
    }

    // true = the action worked
    public bool IsSuccess { get; }

    // What kind of problem, or null on success
    public OperationErrorType? ErrorType { get; }

    // The text to show the user, or null on success
    public string? ErrorMessage { get; }

    // Purpose: Builds a "success" answer.
    // Input:   nothing.
    // Output:  an OperationResult with IsSuccess = true.
    public static OperationResult Success() => new(true, null, null);

    // Purpose: Builds a "failure" answer.
    // Input:   errorType - the kind of problem; errorMessage - the text to show.
    // Output:  an OperationResult with IsSuccess = false.
    public static OperationResult Failure(OperationErrorType errorType, string errorMessage) =>
        new(false, errorType, errorMessage);
}

// The answer of an action that returns a value on success (e.g. create returns the new project).
// "<T>" = the type of that value, chosen where it is used, e.g. OperationResult<Project>
public sealed class OperationResult<T>
{
    // Purpose: Builds an answer. Private - use Success(value) or Failure(...) below.
    // Input:   isSuccess; value (on success); errorType and errorMessage (on failure).
    // Output:  none (a constructor).
    private OperationResult(bool isSuccess, T? value, OperationErrorType? errorType, string? errorMessage)
    {
        IsSuccess = isSuccess;          // did it work?
        Value = value;                  // the value (on success)
        ErrorType = errorType;          // what kind of problem (on failure)
        ErrorMessage = errorMessage;    // the text to show (on failure)
    }

    // true = the action worked
    public bool IsSuccess { get; }

    // The value the action returned, or empty on failure - check IsSuccess first
    public T? Value { get; }

    // What kind of problem, or null on success
    public OperationErrorType? ErrorType { get; }

    // The text to show the user, or null on success
    public string? ErrorMessage { get; }

    // Purpose: Builds a "success" answer with a value.
    // Input:   value - what the action returned.
    // Output:  an OperationResult<T> with IsSuccess = true and the value.
    public static OperationResult<T> Success(T value) => new(true, value, null, null);

    // Purpose: Builds a "failure" answer, without a value.
    // Input:   errorType - the kind of problem; errorMessage - the text to show.
    // Output:  an OperationResult<T> with IsSuccess = false. "default" = the empty value of type T
    public static OperationResult<T> Failure(OperationErrorType errorType, string errorMessage) =>
        new(false, default, errorType, errorMessage);
}
