namespace NetSim.Application.Common;

/// <summary>
/// Categorizes why an application-level operation did not succeed. Kept small and
/// generic on purpose ג€” new categories should only be added once a service actually
/// needs to distinguish them.
/// </summary>
public enum OperationErrorType
{
    /// <summary>The referenced entity (device, connection, ...) does not exist in the current context.</summary>
    NotFound,

    /// <summary>The operation conflicts with existing state.</summary>
    Conflict,

    /// <summary>The input supplied by the caller was invalid.</summary>
    ValidationError,

    /// <summary>The operation cannot run in the application's current state (e.g. no active network).</summary>
    InvalidState,
}
