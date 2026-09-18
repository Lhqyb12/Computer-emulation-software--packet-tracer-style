using NetSim.Core.Devices;

namespace NetSim.Core.Topology;

/// <summary>
/// Structural graph queries over a topology that need a traversal (BFS/DFS) rather than a single
/// index lookup: reachability, shortest structural path, connected components and isolated
/// devices. Kept separate from <see cref="Network"/> itself so the same algorithms run unchanged
/// against the live network or an immutable <see cref="TopologySnapshot"/> (both are
/// <see cref="ITopologyView"/>), and so <see cref="Network"/> stays focused on holding structure
/// rather than analysing it.
///
/// Every result here is <em>topological</em> adjacency only - a path from this service means
/// "there is a sequence of cables", never an IP route. Routing, metrics, VLANs and the like
/// belong to later phases and must not leak in here.
/// </summary>
public interface ITopologyQueryService
{
    /// <summary>True when a sequence of connections links <paramref name="source"/> to <paramref name="destination"/>.</summary>
    bool HasPath(ITopologyView topology, NetworkDevice source, NetworkDevice destination);

    /// <summary>
    /// A shortest device path (fewest hops) from <paramref name="source"/> to
    /// <paramref name="destination"/> inclusive, or an empty list when none exists. A path from a
    /// device to itself is that single device.
    /// </summary>
    IReadOnlyList<NetworkDevice> FindPath(ITopologyView topology, NetworkDevice source, NetworkDevice destination);

    /// <summary>
    /// Partitions every device into connected components. Each isolated device is its own
    /// single-element component. Order within and between components is not significant.
    /// </summary>
    IReadOnlyList<IReadOnlyList<NetworkDevice>> GetConnectedComponents(ITopologyView topology);

    /// <summary>Devices with no connections at all - a structural property, not a protocol diagnostic.</summary>
    IReadOnlyList<NetworkDevice> GetIsolatedDevices(ITopologyView topology);
}
