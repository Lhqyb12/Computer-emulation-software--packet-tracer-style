namespace NetSim.Application.Persistence;

/// <summary>
/// Verifies that the local database is actually reachable and usable, without exposing
/// anything about which storage engine backs it. Intended for a future
/// Settings/Diagnostics screen - not wired into any UI yet.
/// </summary>
public interface IDatabaseHealthCheck
{
    DatabaseHealthStatus Check();
}
