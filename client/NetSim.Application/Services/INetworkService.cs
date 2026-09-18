using NetSim.Core.Topology;

namespace NetSim.Application.Services;

/// <summary>
/// Coordinates creating and clearing the current network/topology. Device and connection
/// operations live in <see cref="IDeviceService"/> and <see cref="IConnectionService"/>
/// respectively, so this service stays focused on the topology's lifecycle.
/// </summary>
public interface INetworkService
{
    /// <summary>The network currently active in the application, or null if none has been created yet.</summary>
    Network? CurrentNetwork { get; }

    /// <summary>
    /// Structural graph queries (reachability, path finding, connected components, isolated
    /// devices) over any <see cref="ITopologyView"/> - typically <see cref="CurrentNetwork"/> or
    /// a snapshot of it. Stateless; the single-index relationship/neighbour queries live directly
    /// on <see cref="Network"/>.
    /// </summary>
    ITopologyQueryService TopologyQuery { get; }

    /// <summary>Creates a new network and makes it the current one.</summary>
    Network CreateNetwork(string name);

    /// <summary>Discards the current network, leaving <see cref="CurrentNetwork"/> null.</summary>
    void ClearNetwork();
}
