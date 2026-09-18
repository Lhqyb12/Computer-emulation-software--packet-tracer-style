namespace NetSim.Application.Routing;

/// <summary>
/// The outcome of validating a <see cref="StaticRouteInput"/> against a router (Phase 28), before
/// any routing-table change is attempted. <see cref="IsValid"/> with no <see cref="Error"/> means
/// the input is safe to add / apply; otherwise <see cref="Error"/> is a user-facing message.
/// </summary>
public sealed class StaticRouteValidationResult
{
    private StaticRouteValidationResult(bool isValid, string? error)
    {
        IsValid = isValid;
        Error = error;
    }

    public bool IsValid { get; }

    public string? Error { get; }

    public static StaticRouteValidationResult Valid { get; } = new(true, null);

    public static StaticRouteValidationResult Invalid(string error) => new(false, error);
}
