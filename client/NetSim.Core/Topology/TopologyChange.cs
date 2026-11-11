using NetSim.Core.Devices;      // NetworkDevice
using NetSim.Core.Networking;   // Connection

namespace NetSim.Core.Topology;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// What kind of change happened in the network.
public enum TopologyChangeKind
{
    DeviceAdded,         // a device was added
    DeviceRemoved,       // a device was removed
    ConnectionAdded,     // a cable was added
    ConnectionRemoved,   // a cable was removed
}

// The "message" the network sends with its events: what changed, and which device or cable.
// The canvas (later) listens to these events and redraws only what changed.
// Exactly one of Device / Connection is filled - the one that matches Kind.
// ": EventArgs" = the standard base for data sent with a .NET event
public sealed class TopologyChangedEventArgs : EventArgs
{
    // Purpose: Builds the message. Private: made only by ForDevice / ForConnection below.
    // Input:   kind - what changed; version - the network's version number after the change;
    //          device / connection - what was added or removed (one of them is null).
    // Output:  none (a constructor).
    private TopologyChangedEventArgs(TopologyChangeKind kind, int version, NetworkDevice? device, Connection? connection)
    {
        Kind = kind;               // what changed
        Version = version;         // the network's version after the change
        Device = device;           // the device, or null
        Connection = connection;   // the cable, or null
    }

    // What changed
    public TopologyChangeKind Kind { get; }

    // The network's version number after this change (it grows by 1 with every change)
    public int Version { get; }

    // The device that was added / removed; null when the change is about a cable
    public NetworkDevice? Device { get; }

    // The cable that was added / removed; null when the change is about a device
    public Connection? Connection { get; }

    // Purpose: Makes a message about a device.
    // Input:   kind - DeviceAdded or DeviceRemoved; version - the new version; device - the device.
    // Output:  the message. "internal" = only code inside NetSim.Core (the Network) makes these
    internal static TopologyChangedEventArgs ForDevice(TopologyChangeKind kind, int version, NetworkDevice device) =>
        new(kind, version, device, null);

    // Purpose: Makes a message about a cable.
    // Input:   kind - ConnectionAdded or ConnectionRemoved; version - the new version; connection - the cable.
    // Output:  the message.
    internal static TopologyChangedEventArgs ForConnection(TopologyChangeKind kind, int version, Connection connection) =>
        new(kind, version, null, connection);
}
