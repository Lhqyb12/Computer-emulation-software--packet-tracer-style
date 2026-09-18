using NetSim.Core.Common;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Topology;

/// <summary>
/// A read-only view of a network's structure: the set of queries every consumer that only wants
/// to <em>inspect</em> the topology (graph traversal, statistics, diagnostics, a future packet or
/// AI layer) depends on, instead of taking a hard dependency on the mutable <see cref="Network"/>.
///
/// Both the live <see cref="Network"/> and an immutable <see cref="TopologySnapshot"/> implement
/// this, so the same query code runs against "the topology right now" or "the topology as it was
/// when the snapshot was taken". It intentionally exposes no mutators - registration and removal
/// stay on <see cref="Network"/>.
/// </summary>
public interface ITopologyView
{
    /// <summary>All devices in the topology. Read-only; order is registration order.</summary>
    IReadOnlyCollection<NetworkDevice> Devices { get; }

    /// <summary>All connections in the topology. Read-only; order is registration order.</summary>
    IReadOnlyCollection<Connection> Connections { get; }

    bool ContainsDevice(EntityId deviceId);

    NetworkDevice? GetDevice(EntityId deviceId);

    /// <summary>The interface with this id, from any device in the topology, or null.</summary>
    NetworkInterface? GetInterface(EntityId interfaceId);

    Connection? GetConnection(EntityId connectionId);

    /// <summary>Every connection with at least one endpoint on <paramref name="device"/>.</summary>
    IReadOnlyCollection<Connection> GetConnections(NetworkDevice device);

    /// <summary>The connection attached to <paramref name="networkInterface"/>, or null if it is not cabled.</summary>
    Connection? GetConnection(NetworkInterface networkInterface);

    /// <summary>Distinct devices directly reachable from <paramref name="device"/> over a single connection.</summary>
    IReadOnlyCollection<NetworkDevice> GetNeighbors(NetworkDevice device);

    /// <summary>The device on the far side of <paramref name="networkInterface"/>'s connection, or null if it is not cabled.</summary>
    NetworkDevice? GetNeighbor(NetworkInterface networkInterface);

    /// <summary>True when at least one connection joins <paramref name="a"/> and <paramref name="b"/> directly.</summary>
    bool AreDirectlyConnected(NetworkDevice a, NetworkDevice b);

    /// <summary>Every connection joining <paramref name="a"/> and <paramref name="b"/> directly (may be more than one).</summary>
    IReadOnlyCollection<Connection> GetConnectionsBetween(NetworkDevice a, NetworkDevice b);
}
