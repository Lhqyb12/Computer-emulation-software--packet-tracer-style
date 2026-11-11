using NetSim.Core.Common;              // EntityId, Guard
using NetSim.Core.Common.Exceptions;   // DomainException
using NetSim.Core.Devices;             // NetworkDevice
using NetSim.Core.Networking;          // NetworkInterface, Connection, ConnectionType

namespace NetSim.Core.Topology;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The whole network of one project: all its devices and all the cables between them.
// This is the one true picture of the network - the canvas, the properties window, and later
// the simulation and the AI all read the network from here.
// Devices and cables enter and leave ONLY through the methods here, so the lists always stay correct.
// "sealed" = no other class can be built on this one
public sealed class Network
{
    // The real lists, in the order things were added
    private readonly List<NetworkDevice> _devices = [];       // all the devices
    private readonly List<Connection> _connections = [];      // all the cables

    // "Dictionaries" (id -> object) over the SAME objects, only to find things by id fast,
    // instead of going over the whole list every time
    private readonly Dictionary<EntityId, NetworkDevice> _devicesById = [];          // device id -> device
    private readonly Dictionary<EntityId, Connection> _connectionsById = [];         // cable id -> cable
    private readonly Dictionary<EntityId, NetworkInterface> _interfacesById = [];    // port id -> port

    // device id -> the cables that have one end on that device
    private readonly Dictionary<EntityId, List<Connection>> _connectionsByDevice = [];

    // Purpose: Builds a new, empty network.
    // Input:   name - the network name.
    // Output:  none (a constructor). Throws DomainException when the name is empty.
    public Network(string name)
    {
        Id = EntityId.New();                                        // a new unique id
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));   // check the name and keep it clean
    }

    // The network's unique id
    public EntityId Id { get; }

    // The network name
    public string Name { get; private set; }

    // A counter that grows by 1 with every change (device or cable added / removed).
    // Code that remembered something about the network can compare it to know "did anything change?"
    public int Version { get; private set; }

    // All the devices, read-only
    public IReadOnlyCollection<NetworkDevice> Devices => _devices.AsReadOnly();

    // All the cables, read-only
    public IReadOnlyCollection<Connection> Connections => _connections.AsReadOnly();

    // Events - "something changed". Whoever listens (later: the canvas) gets a TopologyChangedEventArgs
    public event EventHandler<TopologyChangedEventArgs>? DeviceAdded;         // a device was added
    public event EventHandler<TopologyChangedEventArgs>? DeviceRemoved;       // a device was removed
    public event EventHandler<TopologyChangedEventArgs>? ConnectionAdded;     // a cable was added
    public event EventHandler<TopologyChangedEventArgs>? ConnectionRemoved;   // a cable was removed

    // Sent after EVERY change, together with the specific event above - for code that only needs "refresh"
    public event EventHandler<TopologyChangedEventArgs>? TopologyChanged;

    // Purpose: Changes the network name.
    // Input:   name - the new name.
    // Output:  nothing. Throws DomainException when the name is empty.
    public void Rename(string name)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
    }

    // ----- Devices -----

    // Purpose: Adds a ready device (made by NetworkDeviceFactory) to the network.
    // Input:   device - the device.
    // Output:  nothing. Throws DomainException when a device with the same id is already in the network.
    public void AddDevice(NetworkDevice device)
    {
        // null is not allowed
        ArgumentNullException.ThrowIfNull(device);

        // The same device can't be added twice
        if (_devicesById.ContainsKey(device.Id))
        {
            throw new DomainException($"A device with id '{device.Id}' already exists in the network.");
        }

        _devices.Add(device);                     // put it in the list
        _devicesById.Add(device.Id, device);      // and in the id dictionary
        _connectionsByDevice[device.Id] = [];     // no cables on it yet

        // Put every port of the device in the port dictionary
        foreach (var networkInterface in device.Interfaces)
        {
            _interfacesById[networkInterface.Id] = networkInterface;
        }

        // Listen to the device, so a port added or removed later also updates the port dictionary.
        // "+=" = start listening to the event with this method
        device.InterfaceAdded += OnDeviceInterfaceAdded;
        device.InterfaceRemoved += OnDeviceInterfaceRemoved;

        // One more change
        BumpVersion();

        // Tell the listeners
        RaiseDeviceChange(TopologyChangeKind.DeviceAdded, device, DeviceAdded);
    }

    // Purpose: Removes a device from the network. Every cable on it is removed first,
    //          so no cable is ever left pointing at a device that is gone.
    // Input:   device - the device.
    // Output:  true when it was removed, false when it was not in the network.
    public bool RemoveDevice(NetworkDevice device)
    {
        // null is not allowed
        ArgumentNullException.ThrowIfNull(device);

        // Not in this network - nothing to do
        if (!_devicesById.ContainsKey(device.Id))
        {
            return false;
        }

        // A COPY of its cable list ("ToList"), because removing cables changes the original list while we go over it
        var affectedConnections = _connectionsByDevice[device.Id].ToList();

        // Remove every cable on this device
        foreach (var connection in affectedConnections)
        {
            RemoveConnectionCore(connection);
        }

        // Stop listening to the device. "-=" = stop listening
        device.InterfaceAdded -= OnDeviceInterfaceAdded;
        device.InterfaceRemoved -= OnDeviceInterfaceRemoved;

        // Take its ports out of the port dictionary
        foreach (var networkInterface in device.Interfaces)
        {
            _interfacesById.Remove(networkInterface.Id);
        }

        _connectionsByDevice.Remove(device.Id);   // its cable list
        _devicesById.Remove(device.Id);           // the id dictionary
        _devices.Remove(device);                  // the list

        // One more change
        BumpVersion();

        // Tell the listeners
        RaiseDeviceChange(TopologyChangeKind.DeviceRemoved, device, DeviceRemoved);
        return true;
    }

    // ----- Cables -----

    // Purpose: Connects two ports with a cable - the only way to add a cable to the network.
    //          Checks that both devices are in this network and that these two ports are not already joined;
    //          the other rules (not itself, ports free, cable kinds fit) are checked by Connection.Create.
    // Input:   endpointA, endpointB - the two ports; connectionType - the material (Copper when not given).
    // Output:  the new cable. Throws DomainException when a rule is broken.
    public Connection Connect(
        NetworkInterface endpointA,
        NetworkInterface endpointB,
        ConnectionType connectionType = ConnectionType.Copper)
    {
        // null is not allowed
        ArgumentNullException.ThrowIfNull(endpointA);
        ArgumentNullException.ThrowIfNull(endpointB);

        // The first port's device must be in this network
        if (!_devicesById.ContainsKey(endpointA.Device.Id))
        {
            throw new DomainException($"Device '{endpointA.Device.Name}' is not part of network '{Name}'.");
        }

        // The second port's device must be in this network
        if (!_devicesById.ContainsKey(endpointB.Device.Id))
        {
            throw new DomainException($"Device '{endpointB.Device.Name}' is not part of network '{Name}'.");
        }

        // These two ports must not already be joined
        if (_connections.Any(c => ConnectsSamePair(c, endpointA, endpointB)))
        {
            throw new DomainException("These two interfaces are already connected to each other.");
        }

        // Make the cable (Connection.Create checks the other rules and plugs it into both ports)
        var connection = Connection.Create(endpointA, endpointB, connectionType);

        // A cable between two devices counts as working for now (real up/down comes in step 8)
        connection.BringUp();

        _connections.Add(connection);                                 // put it in the list
        _connectionsById.Add(connection.Id, connection);              // and in the id dictionary
        IndexConnectionForDevice(endpointA.Device.Id, connection);    // on the first device's cable list

        // On the second device's cable list too (unless both ends are on the same device)
        if (endpointA.Device.Id != endpointB.Device.Id)
        {
            IndexConnectionForDevice(endpointB.Device.Id, connection);
        }

        // One more change
        BumpVersion();

        // Tell the listeners, and hand the cable back
        RaiseConnectionChange(TopologyChangeKind.ConnectionAdded, connection, ConnectionAdded);
        return connection;
    }

    // Purpose: Removes a cable: unplugs it from both ports and takes it out of every list.
    // Input:   connection - the cable.
    // Output:  true when it was removed, false when it was not in this network.
    public bool RemoveConnection(Connection connection)
    {
        // null is not allowed
        ArgumentNullException.ThrowIfNull(connection);

        // Not in this network - nothing to do
        if (!_connectionsById.ContainsKey(connection.Id))
        {
            return false;
        }

        // The removal itself
        RemoveConnectionCore(connection);

        // One more change, and tell the listeners
        BumpVersion();
        RaiseConnectionChange(TopologyChangeKind.ConnectionRemoved, connection, ConnectionRemoved);
        return true;
    }

    // Purpose: The removal of a cable, without the version and the events - shared by RemoveConnection
    //          and RemoveDevice (which removes several cables but reports only ONE change: "device removed").
    // Input:   connection - the cable.
    // Output:  nothing.
    private void RemoveConnectionCore(Connection connection)
    {
        // Unplug it from both ports
        connection.Disconnect();

        _connections.Remove(connection);                                       // the list
        _connectionsById.Remove(connection.Id);                                // the id dictionary
        DeindexConnectionForDevice(connection.EndpointA.Device.Id, connection);  // the first device's cable list

        // The second device's cable list (unless both ends are on the same device)
        if (connection.EndpointA.Device.Id != connection.EndpointB.Device.Id)
        {
            DeindexConnectionForDevice(connection.EndpointB.Device.Id, connection);
        }
    }

    // ----- Finding things by id -----

    // Purpose: Is a device with this id in the network.
    // Input:   deviceId - the id.
    // Output:  true / false.
    public bool ContainsDevice(EntityId deviceId) => _devicesById.ContainsKey(deviceId);

    // Purpose: Is this device in the network.
    // Input:   device - the device.
    // Output:  true / false.
    public bool ContainsDevice(NetworkDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);       // null is not allowed
        return _devicesById.ContainsKey(device.Id);      // look it up by its id
    }

    // Purpose: Finds a device by its id.
    // Input:   deviceId - the id.
    // Output:  the device, or null when there is none. "TryGetValue" = look it up without throwing
    public NetworkDevice? GetDevice(EntityId deviceId) =>
        _devicesById.TryGetValue(deviceId, out var device) ? device : null;

    // Purpose: Finds a device by its id, answering "found or not" separately.
    // Input:   deviceId - the id; device - filled by the method ("out").
    // Output:  true + the device, or false.
    public bool TryGetDevice(EntityId deviceId, out NetworkDevice device)
    {
        var found = _devicesById.TryGetValue(deviceId, out var match);   // look it up
        device = match!;                                                 // hand it out ("!" = may be null only when found is false)
        return found;                                                    // found or not
    }

    // Purpose: Finds a port (of any device in the network) by its id.
    // Input:   interfaceId - the id.
    // Output:  the port, or null.
    public NetworkInterface? GetInterface(EntityId interfaceId) =>
        _interfacesById.TryGetValue(interfaceId, out var networkInterface) ? networkInterface : null;

    // Purpose: Finds a cable by its id.
    // Input:   connectionId - the id.
    // Output:  the cable, or null.
    public Connection? GetConnection(EntityId connectionId) =>
        _connectionsById.TryGetValue(connectionId, out var connection) ? connection : null;

    // Purpose: Is a cable with this id in the network.
    // Input:   connectionId - the id.
    // Output:  true / false.
    public bool ContainsConnection(EntityId connectionId) => _connectionsById.ContainsKey(connectionId);

    // ----- Inside helpers -----

    // Purpose: Called by a device when a port is added to it - adds the port to the port dictionary.
    // Input:   sender - the device; networkInterface - the new port.
    // Output:  nothing.
    private void OnDeviceInterfaceAdded(object? sender, NetworkInterface networkInterface) =>
        _interfacesById[networkInterface.Id] = networkInterface;

    // Purpose: Called by a device when a port is removed from it - takes it out of the port dictionary.
    // Input:   sender - the device; networkInterface - the removed port.
    // Output:  nothing.
    private void OnDeviceInterfaceRemoved(object? sender, NetworkInterface networkInterface) =>
        _interfacesById.Remove(networkInterface.Id);

    // Purpose: Adds a cable to a device's cable list (makes the list when it does not exist yet).
    // Input:   deviceId - the device; connection - the cable.
    // Output:  nothing.
    private void IndexConnectionForDevice(EntityId deviceId, Connection connection)
    {
        // No list for this device yet - make one
        if (!_connectionsByDevice.TryGetValue(deviceId, out var list))
        {
            list = [];
            _connectionsByDevice[deviceId] = list;
        }

        // Add the cable
        list.Add(connection);
    }

    // Purpose: Takes a cable out of a device's cable list.
    // Input:   deviceId - the device; connection - the cable.
    // Output:  nothing.
    private void DeindexConnectionForDevice(EntityId deviceId, Connection connection)
    {
        // Only when the device has a list
        if (_connectionsByDevice.TryGetValue(deviceId, out var list))
        {
            list.Remove(connection);
        }
    }

    // Purpose: Grows the version number by 1.
    // Input:   nothing.
    // Output:  nothing.
    private void BumpVersion() => Version++;

    // Purpose: Sends the events about a device change: the specific one, then TopologyChanged.
    // Input:   kind - what changed; device - the device; specific - DeviceAdded or DeviceRemoved.
    // Output:  nothing.
    private void RaiseDeviceChange(TopologyChangeKind kind, NetworkDevice device, EventHandler<TopologyChangedEventArgs>? specific)
    {
        var args = TopologyChangedEventArgs.ForDevice(kind, Version, device);   // the message
        specific?.Invoke(this, args);                                           // the specific event ("?." = only when someone listens)
        TopologyChanged?.Invoke(this, args);                                    // the general event
    }

    // Purpose: Sends the events about a cable change: the specific one, then TopologyChanged.
    // Input:   kind - what changed; connection - the cable; specific - ConnectionAdded or ConnectionRemoved.
    // Output:  nothing.
    private void RaiseConnectionChange(TopologyChangeKind kind, Connection connection, EventHandler<TopologyChangedEventArgs>? specific)
    {
        var args = TopologyChangedEventArgs.ForConnection(kind, Version, connection);   // the message
        specific?.Invoke(this, args);                                                   // the specific event
        TopologyChanged?.Invoke(this, args);                                            // the general event
    }

    // Purpose: Does this cable join exactly these two ports (in either order).
    // Input:   connection - the cable; a, b - the two ports.
    // Output:  true / false.
    private static bool ConnectsSamePair(Connection connection, NetworkInterface a, NetworkInterface b) =>
        (ReferenceEquals(connection.EndpointA, a) && ReferenceEquals(connection.EndpointB, b)) ||   // A-B
        (ReferenceEquals(connection.EndpointA, b) && ReferenceEquals(connection.EndpointB, a));     // or B-A
}
