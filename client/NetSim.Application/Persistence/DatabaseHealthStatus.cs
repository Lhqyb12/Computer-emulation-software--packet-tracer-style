namespace NetSim.Application.Persistence;

/// <summary>Result of a lightweight check that the local database is reachable and usable.</summary>
public sealed class DatabaseHealthStatus
{
    private DatabaseHealthStatus(bool isHealthy, string? errorMessage)
    {
        IsHealthy = isHealthy;
        ErrorMessage = errorMessage;
    }

    public bool IsHealthy { get; }

    public string? ErrorMessage { get; }

    public static DatabaseHealthStatus Healthy() => new(true, null);

    public static DatabaseHealthStatus Unhealthy(string errorMessage) => new(false, errorMessage);
}
