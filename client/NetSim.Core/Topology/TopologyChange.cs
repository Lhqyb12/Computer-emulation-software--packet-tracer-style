using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Topology;

/// <summary>
/// The kind of structural mutation a <see cref="TopologyChangedEventArgs"/> describes.
/// </summary>
public enum TopologyChangeKind
{
    DeviceAdded,
    DeviceRemoved,
    ConnectionAdded,
    ConnectionRemoved,
}

/// <summary>
/// Payload for <see cref="Network.TopologyChanged"/> and the more specific
/// <see cref="Network.DeviceAdded"/>/<see cref="Network.DeviceRemoved"/>/
/// <see cref="Network.ConnectionAdded"/>/<see cref="Network.ConnectionRemoved"/> events.
///
/// This is a plain data carrier so higher layers (a ViewModel, a future monitor, a diagnostics
/// panel) can react to exactly what changed without re-diffing the whole topology, and without
/// the Core layer taking any dependency on how those layers dispatch notifications. Exactly one
/// of <see cref="Device"/> / <see cref="Connection"/> is non-null, matching <see cref="Kind"/>.
/// </summary>
public sealed class TopologyChangedEventArgs : EventArgs
{
    private TopologyChangedEventArgs(TopologyChangeKind kind, int version, NetworkDevice? device, Connection? connection)
    {
        Kind = kind;
        Version = version;
        Device = device;
        Connection = connection;
    }

    public TopologyChangeKind Kind { get; }

    /// <summary>The <see cref="Network.Version"/> the topology reached as a result of this change.</summary>
    public int Version { get; }

    /// <summary>The device involved, for <see cref="TopologyChangeKind.DeviceAdded"/>/<see cref="TopologyChangeKind.DeviceRemoved"/>; otherwise null.</summary>
    public NetworkDevice? Device { get; }

    /// <summary>The connection involved, for <see cref="TopologyChangeKind.ConnectionAdded"/>/<see cref="TopologyChangeKind.ConnectionRemoved"/>; otherwise null.</summary>
    public Connection? Connection { get; }

    internal static TopologyChangedEventArgs ForDevice(TopologyChangeKind kind, int version, NetworkDevice device) =>
        new(kind, version, device, null);

    internal static TopologyChangedEventArgs ForConnection(TopologyChangeKind kind, int version, Connection connection) =>
        new(kind, version, null, connection);
}
