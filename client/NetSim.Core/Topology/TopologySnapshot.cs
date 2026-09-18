using NetSim.Core.Common;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Topology;

/// <summary>
/// An immutable structural view of a <see cref="Network"/> taken at one instant.
///
/// "Immutable" here means the <em>structure</em> is frozen: which devices, interfaces and
/// connections are part of the topology, and the indexes/adjacency over them, will not change
/// even if the originating <see cref="Network"/> is edited afterwards. It intentionally does not
/// deep-copy the domain objects - it holds the same <see cref="NetworkDevice"/>/
/// <see cref="Connection"/> references - so it stays cheap to create. It is the stable input a
/// future simulation / diagnostics / AI pass can iterate over without racing live edits, and it
/// satisfies the same <see cref="ITopologyView"/> contract as the live network, so query code is
/// written once.
/// </summary>
public sealed class TopologySnapshot : ITopologyView
{
    private readonly IReadOnlyList<NetworkDevice> _devices;
    private readonly IReadOnlyList<Connection> _connections;
    private readonly Dictionary<EntityId, NetworkDevice> _devicesById;
    private readonly Dictionary<EntityId, NetworkInterface> _interfacesById;
    private readonly Dictionary<EntityId, Connection> _connectionsById;
    private readonly Dictionary<EntityId, List<Connection>> _connectionsByDevice;

    private TopologySnapshot(
        EntityId networkId,
        string networkName,
        int version,
        IReadOnlyList<NetworkDevice> devices,
        IReadOnlyList<Connection> connections,
        Dictionary<EntityId, NetworkDevice> devicesById,
        Dictionary<EntityId, NetworkInterface> interfacesById,
        Dictionary<EntityId, Connection> connectionsById,
        Dictionary<EntityId, List<Connection>> connectionsByDevice)
    {
        NetworkId = networkId;
        NetworkName = networkName;
        Version = version;
        _devices = devices;
        _connections = connections;
        _devicesById = devicesById;
        _interfacesById = interfacesById;
        _connectionsById = connectionsById;
        _connectionsByDevice = connectionsByDevice;
    }

    public EntityId NetworkId { get; }

    public string NetworkName { get; }

    /// <summary>The <see cref="Network.Version"/> the source network was at when this snapshot was captured.</summary>
    public int Version { get; }

    public IReadOnlyCollection<NetworkDevice> Devices => _devices;

    public IReadOnlyCollection<Connection> Connections => _connections;

    internal static TopologySnapshot Capture(Network network)
    {
        var devices = network.Devices.ToList();
        var connections = network.Connections.ToList();

        var devicesById = devices.ToDictionary(d => d.Id);
        var interfacesById = new Dictionary<EntityId, NetworkInterface>();
        var connectionsByDevice = devices.ToDictionary(d => d.Id, _ => new List<Connection>());

        foreach (var device in devices)
        {
            foreach (var networkInterface in device.Interfaces)
            {
                interfacesById[networkInterface.Id] = networkInterface;
            }
        }

        foreach (var connection in connections)
        {
            AddIfKnown(connectionsByDevice, connection.EndpointA.Device.Id, connection);
            if (connection.EndpointA.Device.Id != connection.EndpointB.Device.Id)
            {
                AddIfKnown(connectionsByDevice, connection.EndpointB.Device.Id, connection);
            }
        }

        return new TopologySnapshot(
            network.Id,
            network.Name,
            network.Version,
            devices.AsReadOnly(),
            connections.AsReadOnly(),
            devicesById,
            interfacesById,
            connections.ToDictionary(c => c.Id),
            connectionsByDevice);

        static void AddIfKnown(Dictionary<EntityId, List<Connection>> map, EntityId deviceId, Connection connection)
        {
            if (map.TryGetValue(deviceId, out var list))
            {
                list.Add(connection);
            }
        }
    }

    public bool ContainsDevice(EntityId deviceId) => _devicesById.ContainsKey(deviceId);

    public NetworkDevice? GetDevice(EntityId deviceId) =>
        _devicesById.TryGetValue(deviceId, out var device) ? device : null;

    public NetworkInterface? GetInterface(EntityId interfaceId) =>
        _interfacesById.TryGetValue(interfaceId, out var networkInterface) ? networkInterface : null;

    public Connection? GetConnection(EntityId connectionId) =>
        _connectionsById.TryGetValue(connectionId, out var connection) ? connection : null;

    public IReadOnlyCollection<Connection> GetConnections(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _connectionsByDevice.TryGetValue(device.Id, out var list) ? list.AsReadOnly() : [];
    }

    public Connection? GetConnection(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);
        return networkInterface.Connection;
    }

    public IReadOnlyCollection<NetworkDevice> GetNeighbors(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_connectionsByDevice.TryGetValue(device.Id, out var list) || list.Count == 0)
        {
            return [];
        }

        var seen = new HashSet<EntityId> { device.Id };
        var neighbors = new List<NetworkDevice>();
        foreach (var connection in list)
        {
            var other = OtherDevice(connection, device);
            if (seen.Add(other.Id))
            {
                neighbors.Add(other);
            }
        }

        return neighbors.AsReadOnly();
    }

    public NetworkDevice? GetNeighbor(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);
        return networkInterface.Connection?.GetOtherEndpoint(networkInterface).Device;
    }

    public bool AreDirectlyConnected(NetworkDevice a, NetworkDevice b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Id == b.Id || !_connectionsByDevice.TryGetValue(a.Id, out var list))
        {
            return false;
        }

        return list.Any(c => OtherDevice(c, a).Id == b.Id);
    }

    public IReadOnlyCollection<Connection> GetConnectionsBetween(NetworkDevice a, NetworkDevice b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a.Id == b.Id || !_connectionsByDevice.TryGetValue(a.Id, out var list))
        {
            return [];
        }

        return list.Where(c => OtherDevice(c, a).Id == b.Id).ToList().AsReadOnly();
    }

    private static NetworkDevice OtherDevice(Connection connection, NetworkDevice device) =>
        connection.EndpointA.Device.Id == device.Id ? connection.EndpointB.Device : connection.EndpointA.Device;
}
