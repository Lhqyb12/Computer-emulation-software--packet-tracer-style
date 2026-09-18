using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Topology;

/// <summary>
/// The network topology engine: the single, authoritative model of how the network is
/// structured. Everything else - the canvas, device properties, a future packet/routing/AI
/// layer - reads structure from here rather than deriving it from anything visual.
///
/// Conceptually a graph: each <see cref="NetworkDevice"/> is a node, each
/// <see cref="NetworkInterface"/> is a port on a node, and each <see cref="Connection"/> is an
/// edge joining two ports. The topology keeps the interface-level relationship intact
/// (device -&gt; interface -&gt; connection -&gt; interface -&gt; device) rather than collapsing to
/// device-to-device edges, and it makes no assumption that the graph is a tree: cycles and
/// (in principle) multiple connections between the same device pair are both supported.
///
/// Devices and connections only ever enter or leave through this type's controlled operations,
/// which keep three id indexes (device / interface / connection) and a per-device connection
/// index current so the common lookups stay O(1)/O(degree) rather than O(n) scans. Every
/// mutation bumps <see cref="Version"/> and raises the matching change events.
/// </summary>
public sealed class Network : ITopologyView
{
    // Ordered canonical collections (registration order, as before). The dictionaries alongside
    // them are pure indexes over the same object references - never a second source of truth.
    private readonly List<NetworkDevice> _devices = [];
    private readonly List<Connection> _connections = [];

    private readonly Dictionary<EntityId, NetworkDevice> _devicesById = [];
    private readonly Dictionary<EntityId, Connection> _connectionsById = [];
    private readonly Dictionary<EntityId, NetworkInterface> _interfacesById = [];

    // device id -> the connections with an endpoint on that device. Keeps GetConnections /
    // GetNeighbors / AreDirectlyConnected off a full connection scan.
    private readonly Dictionary<EntityId, List<Connection>> _connectionsByDevice = [];

    public Network(string name)
    {
        Id = EntityId.New();
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
    }

    public EntityId Id { get; }

    public string Name { get; private set; }

    /// <summary>
    /// Monotonically increasing revision counter. Bumped once per successful structural mutation
    /// (device or connection added/removed). A consumer that cached anything derived from the
    /// topology can compare this to know whether it must recompute - "has the topology changed?".
    /// </summary>
    public int Version { get; private set; }

    public IReadOnlyCollection<NetworkDevice> Devices => _devices.AsReadOnly();

    public IReadOnlyCollection<Connection> Connections => _connections.AsReadOnly();

    /// <summary>Raised after a device is registered. Also raises <see cref="TopologyChanged"/>.</summary>
    public event EventHandler<TopologyChangedEventArgs>? DeviceAdded;

    /// <summary>Raised after a device (and any connections it carried) is removed. Also raises <see cref="TopologyChanged"/>.</summary>
    public event EventHandler<TopologyChangedEventArgs>? DeviceRemoved;

    /// <summary>Raised after a connection is registered. Also raises <see cref="TopologyChanged"/>.</summary>
    public event EventHandler<TopologyChangedEventArgs>? ConnectionAdded;

    /// <summary>Raised after a connection is removed. Also raises <see cref="TopologyChanged"/>.</summary>
    public event EventHandler<TopologyChangedEventArgs>? ConnectionRemoved;

    /// <summary>
    /// Raised after every structural mutation, in addition to the specific event above. A
    /// consumer that just needs "something changed, refresh" subscribes here; one that wants to
    /// react surgically subscribes to the specific events.
    /// </summary>
    public event EventHandler<TopologyChangedEventArgs>? TopologyChanged;

    public void Rename(string name)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
    }

    // ---------------------------------------------------------------------------------------
    // Device registration / removal
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Registers an already-constructed device as part of this topology. Rejects a device whose
    /// id is already present (an interface therefore can never end up owned by two devices in the
    /// same topology, since it comes in attached to exactly one).
    /// </summary>
    public void AddDevice(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (_devicesById.ContainsKey(device.Id))
        {
            throw new DomainException($"A device with id '{device.Id}' already exists in the network.");
        }

        _devices.Add(device);
        _devicesById.Add(device.Id, device);
        _connectionsByDevice[device.Id] = [];

        foreach (var networkInterface in device.Interfaces)
        {
            _interfacesById[networkInterface.Id] = networkInterface;
        }

        // Keep the interface index current even for interfaces added after registration.
        device.InterfaceAdded += OnDeviceInterfaceAdded;
        device.InterfaceRemoved += OnDeviceInterfaceRemoved;

        BumpVersion();
        RaiseDeviceChange(TopologyChangeKind.DeviceAdded, device, DeviceAdded);
    }

    /// <summary>
    /// Removes a device. Any connection with an endpoint on it is removed first (detaching the
    /// far interface), so no connection is ever left pointing at a device outside the topology.
    /// Returns false when the device was not registered.
    /// </summary>
    public bool RemoveDevice(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!_devicesById.ContainsKey(device.Id))
        {
            return false;
        }

        // Copy first: RemoveConnection mutates the per-device connection list.
        var affectedConnections = _connectionsByDevice[device.Id].ToList();
        foreach (var connection in affectedConnections)
        {
            RemoveConnectionCore(connection);
        }

        device.InterfaceAdded -= OnDeviceInterfaceAdded;
        device.InterfaceRemoved -= OnDeviceInterfaceRemoved;

        foreach (var networkInterface in device.Interfaces)
        {
            _interfacesById.Remove(networkInterface.Id);
        }

        _connectionsByDevice.Remove(device.Id);
        _devicesById.Remove(device.Id);
        _devices.Remove(device);

        BumpVersion();
        RaiseDeviceChange(TopologyChangeKind.DeviceRemoved, device, DeviceRemoved);
        return true;
    }

    // ---------------------------------------------------------------------------------------
    // Connection registration / removal
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// The one and only way to create a <see cref="Connection"/> that counts as part of this
    /// topology. Verifies both endpoints belong to devices registered here and that this exact
    /// interface pair is not already connected, then delegates the structural rules
    /// (self-connect, endpoint already connected, incompatible media) to <see cref="Connection.Create"/>.
    /// </summary>
    public Connection Connect(
        NetworkInterface endpointA,
        NetworkInterface endpointB,
        ConnectionType connectionType = ConnectionType.Copper)
    {
        ArgumentNullException.ThrowIfNull(endpointA);
        ArgumentNullException.ThrowIfNull(endpointB);

        if (!_devicesById.ContainsKey(endpointA.Device.Id))
        {
            throw new DomainException($"Device '{endpointA.Device.Name}' is not part of network '{Name}'.");
        }

        if (!_devicesById.ContainsKey(endpointB.Device.Id))
        {
            throw new DomainException($"Device '{endpointB.Device.Name}' is not part of network '{Name}'.");
        }

        if (_connections.Any(c => ConnectsSamePair(c, endpointA, endpointB)))
        {
            throw new DomainException("These two interfaces are already connected to each other.");
        }

        var connection = Connection.Create(endpointA, endpointB, connectionType);

        // A cable physically present between two devices reads as an "up" link; speed/duplex
        // negotiation and per-interface line state belong to a later phase.
        connection.BringUp();

        _connections.Add(connection);
        _connectionsById.Add(connection.Id, connection);
        IndexConnectionForDevice(endpointA.Device.Id, connection);
        if (endpointA.Device.Id != endpointB.Device.Id)
        {
            IndexConnectionForDevice(endpointB.Device.Id, connection);
        }

        BumpVersion();
        RaiseConnectionChange(TopologyChangeKind.ConnectionAdded, connection, ConnectionAdded);
        return connection;
    }

    /// <summary>
    /// Removes a connection: detaches both interfaces and drops it from every index, so topology
    /// queries reflect the change immediately. Returns false when it was not registered here.
    /// </summary>
    public bool RemoveConnection(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (!_connectionsById.ContainsKey(connection.Id))
        {
            return false;
        }

        RemoveConnectionCore(connection);

        BumpVersion();
        RaiseConnectionChange(TopologyChangeKind.ConnectionRemoved, connection, ConnectionRemoved);
        return true;
    }

    // Shared teardown with no versioning/eventing, so RemoveDevice can cascade several
    // connection removals and surface them as a single DeviceRemoved change.
    private void RemoveConnectionCore(Connection connection)
    {
        connection.Disconnect();

        _connections.Remove(connection);
        _connectionsById.Remove(connection.Id);
        DeindexConnectionForDevice(connection.EndpointA.Device.Id, connection);
        if (connection.EndpointA.Device.Id != connection.EndpointB.Device.Id)
        {
            DeindexConnectionForDevice(connection.EndpointB.Device.Id, connection);
        }
    }

    // ---------------------------------------------------------------------------------------
    // Lookups (ITopologyView)
    // ---------------------------------------------------------------------------------------

    public bool ContainsDevice(EntityId deviceId) => _devicesById.ContainsKey(deviceId);

    public bool ContainsDevice(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _devicesById.ContainsKey(device.Id);
    }

    public NetworkDevice? GetDevice(EntityId deviceId) =>
        _devicesById.TryGetValue(deviceId, out var device) ? device : null;

    public bool TryGetDevice(EntityId deviceId, out NetworkDevice device)
    {
        var found = _devicesById.TryGetValue(deviceId, out var match);
        device = match!;
        return found;
    }

    public NetworkInterface? GetInterface(EntityId interfaceId) =>
        _interfacesById.TryGetValue(interfaceId, out var networkInterface) ? networkInterface : null;

    public Connection? GetConnection(EntityId connectionId) =>
        _connectionsById.TryGetValue(connectionId, out var connection) ? connection : null;

    public bool ContainsConnection(EntityId connectionId) => _connectionsById.ContainsKey(connectionId);

    // ---------------------------------------------------------------------------------------
    // Relationship queries
    // ---------------------------------------------------------------------------------------

    /// <summary>The interfaces belonging to <paramref name="device"/> (its own, single-owner list).</summary>
    public IReadOnlyCollection<NetworkInterface> GetInterfaces(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        RequireRegistered(device);
        return device.Interfaces;
    }

    public IReadOnlyCollection<Connection> GetConnections(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _connectionsByDevice.TryGetValue(device.Id, out var list)
            ? list.AsReadOnly()
            : [];
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

        // Distinct by id, and never the device itself (a hypothetical cable between two ports of
        // the same device is not a "neighbour").
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

    /// <summary>
    /// The connection joining exactly these two interfaces, or null if they are not directly
    /// connected. Order-independent. Kept for call sites that key on the interface pair rather
    /// than the device pair.
    /// </summary>
    public Connection? FindConnection(NetworkInterface endpointA, NetworkInterface endpointB)
    {
        ArgumentNullException.ThrowIfNull(endpointA);
        ArgumentNullException.ThrowIfNull(endpointB);

        return _connections.FirstOrDefault(c => ConnectsSamePair(c, endpointA, endpointB));
    }

    // ---------------------------------------------------------------------------------------
    // Statistics / validation / snapshot
    // ---------------------------------------------------------------------------------------

    public TopologyStatistics GetStatistics()
    {
        var connectedDeviceCount = _devices.Count(d => _connectionsByDevice[d.Id].Count > 0);
        return new TopologyStatistics(
            DeviceCount: _devices.Count,
            InterfaceCount: _devices.Sum(d => d.Interfaces.Count),
            ConnectionCount: _connections.Count,
            ConnectedDeviceCount: connectedDeviceCount,
            IsolatedDeviceCount: _devices.Count - connectedDeviceCount,
            ConnectedComponentCount: CountConnectedComponents());
    }

    /// <summary>
    /// Checks the topology's structural invariants and returns every problem found, without
    /// throwing. A healthy topology returns <see cref="TopologyValidationResult.IsValid"/> == true.
    /// </summary>
    public TopologyValidationResult Validate()
    {
        var issues = new List<TopologyValidationIssue>();

        // Every interface's declared owner must be a registered device that actually holds it.
        foreach (var device in _devices)
        {
            foreach (var networkInterface in device.Interfaces)
            {
                if (!ReferenceEquals(networkInterface.Device, device))
                {
                    issues.Add(new TopologyValidationIssue(
                        TopologyValidationIssueKind.InvalidInterfaceOwnership,
                        $"Interface '{networkInterface.Name}' is listed on device '{device.Name}' but reports a different owner."));
                }
            }
        }

        var seenConnectionIds = new HashSet<EntityId>();
        var seenInterfacePairs = new HashSet<(EntityId, EntityId)>();
        foreach (var connection in _connections)
        {
            if (!seenConnectionIds.Add(connection.Id))
            {
                issues.Add(new TopologyValidationIssue(
                    TopologyValidationIssueKind.DuplicateConnection,
                    $"Connection '{connection.Id}' is registered more than once."));
            }

            var a = connection.EndpointA;
            var b = connection.EndpointB;

            if (ReferenceEquals(a, b))
            {
                issues.Add(new TopologyValidationIssue(
                    TopologyValidationIssueKind.SelfConnection,
                    $"Connection '{connection.Id}' joins interface '{a.Name}' to itself."));
            }

            var pairKey = a.Id.Value.CompareTo(b.Id.Value) <= 0 ? (a.Id, b.Id) : (b.Id, a.Id);
            if (!seenInterfacePairs.Add(pairKey))
            {
                issues.Add(new TopologyValidationIssue(
                    TopologyValidationIssueKind.DuplicateConnection,
                    $"More than one connection joins interfaces '{a.Name}' and '{b.Name}'."));
            }

            foreach (var endpoint in new[] { a, b })
            {
                if (!_devicesById.ContainsKey(endpoint.Device.Id))
                {
                    issues.Add(new TopologyValidationIssue(
                        TopologyValidationIssueKind.OrphanedConnection,
                        $"Connection '{connection.Id}' has an endpoint on device '{endpoint.Device.Name}', which is not in the topology."));
                }

                if (!ReferenceEquals(endpoint.Connection, connection))
                {
                    issues.Add(new TopologyValidationIssue(
                        TopologyValidationIssueKind.InterfaceConnectionMismatch,
                        $"Interface '{endpoint.Name}' does not reference the connection '{connection.Id}' it is an endpoint of."));
                }
            }
        }

        // Every interface that thinks it is connected must point at a connection we track.
        foreach (var device in _devices)
        {
            foreach (var networkInterface in device.Interfaces)
            {
                if (networkInterface.Connection is { } connection && !_connectionsById.ContainsKey(connection.Id))
                {
                    issues.Add(new TopologyValidationIssue(
                        TopologyValidationIssueKind.UntrackedConnection,
                        $"Interface '{networkInterface.Name}' on device '{device.Name}' references connection '{connection.Id}', which the topology does not track."));
                }
            }
        }

        return issues.Count == 0 ? TopologyValidationResult.Valid : new TopologyValidationResult(issues);
    }

    /// <summary>
    /// Captures the current structure as an immutable <see cref="TopologySnapshot"/> - a
    /// lightweight, stable copy that a simulation / diagnostics / AI pass can reason about
    /// without racing against further edits to this network.
    /// </summary>
    public TopologySnapshot CreateSnapshot() => TopologySnapshot.Capture(this);

    // ---------------------------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------------------------

    private void OnDeviceInterfaceAdded(object? sender, NetworkInterface networkInterface) =>
        _interfacesById[networkInterface.Id] = networkInterface;

    private void OnDeviceInterfaceRemoved(object? sender, NetworkInterface networkInterface) =>
        _interfacesById.Remove(networkInterface.Id);

    private void IndexConnectionForDevice(EntityId deviceId, Connection connection)
    {
        if (!_connectionsByDevice.TryGetValue(deviceId, out var list))
        {
            list = [];
            _connectionsByDevice[deviceId] = list;
        }

        list.Add(connection);
    }

    private void DeindexConnectionForDevice(EntityId deviceId, Connection connection)
    {
        if (_connectionsByDevice.TryGetValue(deviceId, out var list))
        {
            list.Remove(connection);
        }
    }

    private void RequireRegistered(NetworkDevice device)
    {
        if (!_devicesById.ContainsKey(device.Id))
        {
            throw new DomainException($"Device '{device.Name}' is not part of network '{Name}'.");
        }
    }

    private void BumpVersion() => Version++;

    private void RaiseDeviceChange(TopologyChangeKind kind, NetworkDevice device, EventHandler<TopologyChangedEventArgs>? specific)
    {
        var args = TopologyChangedEventArgs.ForDevice(kind, Version, device);
        specific?.Invoke(this, args);
        TopologyChanged?.Invoke(this, args);
    }

    private void RaiseConnectionChange(TopologyChangeKind kind, Connection connection, EventHandler<TopologyChangedEventArgs>? specific)
    {
        var args = TopologyChangedEventArgs.ForConnection(kind, Version, connection);
        specific?.Invoke(this, args);
        TopologyChanged?.Invoke(this, args);
    }

    private int CountConnectedComponents()
    {
        if (_devices.Count == 0)
        {
            return 0;
        }

        var visited = new HashSet<EntityId>();
        var components = 0;
        var queue = new Queue<NetworkDevice>();

        foreach (var start in _devices)
        {
            if (!visited.Add(start.Id))
            {
                continue;
            }

            components++;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in GetNeighbors(current))
                {
                    if (visited.Add(neighbor.Id))
                    {
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }

        return components;
    }

    private static NetworkDevice OtherDevice(Connection connection, NetworkDevice device) =>
        connection.EndpointA.Device.Id == device.Id ? connection.EndpointB.Device : connection.EndpointA.Device;

    private static bool ConnectsSamePair(Connection connection, NetworkInterface a, NetworkInterface b) =>
        (ReferenceEquals(connection.EndpointA, a) && ReferenceEquals(connection.EndpointB, b)) ||
        (ReferenceEquals(connection.EndpointA, b) && ReferenceEquals(connection.EndpointB, a));
}
