using NetSim.Core.Common;              // EntityId, Guard
using NetSim.Core.Common.Exceptions;   // DomainException
using NetSim.Core.Networking;          // NetworkInterface, InterfaceType

namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The base of every device in a network (PC, laptop, server, router, switch).
// Holds what every device has: an id, a name, a type, on/off, and its list of network ports.
// "abstract" = nobody builds a plain NetworkDevice - only a real kind built on it (Pc, Router...)
public abstract class NetworkDevice
{
    // The real list of ports. Private: outside code can't add or remove ports directly -
    // only through AddInterface / RemoveInterface below. "[]" = start with an empty list
    private readonly List<NetworkInterface> _interfaces = [];

    // Purpose: Builds a brand-new device with a new id.
    // Input:   name - the device name; deviceType - which kind of device.
    // Output:  none (a constructor). "protected" = only the device kinds built on this class can call it.
    //          ": this(...)" = pass on to the second constructor below, with a new id
    protected NetworkDevice(string name, DeviceType deviceType)
        : this(EntityId.New(), name, deviceType)
    {
    }

    // Purpose: Builds a device with an id that already exists - only for loading a saved network.
    // Input:   id - the saved id; name - the device name; deviceType - which kind of device.
    // Output:  none (a constructor). Throws DomainException when the name is empty.
    protected NetworkDevice(EntityId id, string name, DeviceType deviceType)
    {
        Id = id;                                                    // keep the id
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));   // check the name and keep it clean
        DeviceType = deviceType;                                    // keep the kind
        OperationalState = DeviceOperationalState.PoweredOff;       // every new device starts switched off
    }

    // The device's unique id
    public EntityId Id { get; }

    // Events: "a port was added / removed". The network (step 1.9) listens to them so its
    // list of all ports stays up to date. "?" = may have no listeners
    public event EventHandler<NetworkInterface>? InterfaceAdded;
    public event EventHandler<NetworkInterface>? InterfaceRemoved;

    // The device name. "private set" = everyone can read it, only this class can change it (through Rename)
    public string Name { get; private set; }

    // Which kind of device (Router, Switch, Pc...). Never changes
    public DeviceType DeviceType { get; }

    // Switched on or off
    public DeviceOperationalState OperationalState { get; private set; }

    // The ports, read-only: outside code can look at them and loop over them, but not change the list
    public IReadOnlyCollection<NetworkInterface> Interfaces => _interfaces.AsReadOnly();

    // Purpose: Changes the device name.
    // Input:   name - the new name.
    // Output:  nothing. Throws DomainException when the name is empty.
    public void Rename(string name)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));   // check it and keep it clean
    }

    // Purpose: Switches the device on.
    // Input:   nothing.
    // Output:  nothing.
    public void PowerOn()
    {
        OperationalState = DeviceOperationalState.PoweredOn;
    }

    // Purpose: Switches the device off.
    // Input:   nothing.
    // Output:  nothing.
    public void PowerOff()
    {
        OperationalState = DeviceOperationalState.PoweredOff;
    }

    // Purpose: Adds a new port to this device. The ONLY way a port is ever created,
    //          so every port always belongs to exactly one device.
    // Input:   name - the port name (e.g. "FastEthernet0/1"); interfaceType - the port kind.
    // Output:  the new port. Throws DomainException when the name is empty or already used on this device.
    public NetworkInterface AddInterface(string name, InterfaceType interfaceType)
    {
        // Check the name and clean it
        var validatedName = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

        // Two ports on the same device can't have the same name (ignoring upper/lower case).
        // "Any(i => ...)" = is there at least one port in the list for which this is true
        if (_interfaces.Any(i => i.Name.Equals(validatedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"Device '{Name}' already has an interface named '{validatedName}'.");
        }

        // Build the port, with "this" (this device) as its owner
        var networkInterface = new NetworkInterface(validatedName, interfaceType, this);

        // Put it in the list
        _interfaces.Add(networkInterface);

        // Tell whoever listens that a port was added. "?." = only when someone listens
        InterfaceAdded?.Invoke(this, networkInterface);

        // Hand the new port back
        return networkInterface;
    }

    // Purpose: Removes a port from this device.
    // Input:   networkInterface - the port to remove.
    // Output:  true when it was removed, false when it was not on this device.
    //          Throws DomainException when a cable is still plugged into it.
    public bool RemoveInterface(NetworkInterface networkInterface)
    {
        // null is not allowed - throw right away
        ArgumentNullException.ThrowIfNull(networkInterface);

        // A port with a cable in it can't be removed - remove the cable first
        if (networkInterface.IsConnected)
        {
            throw new DomainException(
                $"Interface '{networkInterface.Name}' on device '{Name}' is still connected; remove the connection first.");
        }

        // Take it out of the list. "Remove" returns true when it was found
        var removed = _interfaces.Remove(networkInterface);

        // Removed - tell whoever listens
        if (removed)
        {
            InterfaceRemoved?.Invoke(this, networkInterface);
        }

        // Say whether it was removed
        return removed;
    }
}
