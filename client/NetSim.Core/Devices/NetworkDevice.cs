using NetSim.Core.Common;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Devices;

/// <summary>
/// Base abstraction for every device that can participate in a network topology
/// (routers, switches, end devices, ...). Owns its own collection of interfaces so
/// that "which interfaces belong to which device" always has a single source of truth.
/// </summary>
public abstract class NetworkDevice
{
    // The REAL, mutable list of interfaces lives here, private - nobody outside
    // this class can add/remove items directly. Only the controlled methods below
    // (AddInterface / RemoveInterface) are allowed to touch it.
    private readonly List<NetworkInterface> _interfaces = [];

    // "protected" = only this class itself, or a class that inherits from it
    // (Router, Switch, EndDevice...), can call this constructor. Nobody can write
    // "new NetworkDevice(...)" directly, because the class is abstract anyway.
    protected NetworkDevice(string name, DeviceType deviceType)
        : this(EntityId.New(), name, deviceType)
    {
    }

    // Overload used only for persistence rehydration (see NetworkDeviceFactory's
    // "rehydrate" entry point), where the device already has an id from a previous save and
    // must come back with that exact same id rather than a freshly generated one. Ordinary
    // device creation always goes through the (string, DeviceType) constructor above, which
    // mints a brand-new id via EntityId.New() - this overload is never a second way to create
    // a new, original device.
    protected NetworkDevice(EntityId id, string name, DeviceType deviceType)
    {
        Id = id;
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        DeviceType = deviceType;
        OperationalState = DeviceOperationalState.PoweredOff;
    }

    public EntityId Id { get; }

    /// <summary>
    /// Raised after an interface has been added to this device. The topology engine
    /// (<see cref="Topology.Network"/>) subscribes to this so its interface-id index stays
    /// current even when interfaces are added to a device that is already registered. This is a
    /// plain domain event - it carries no UI or persistence concern.
    /// </summary>
    public event EventHandler<NetworkInterface>? InterfaceAdded;

    /// <summary>Raised after an interface has been removed from this device. See <see cref="InterfaceAdded"/>.</summary>
    public event EventHandler<NetworkInterface>? InterfaceRemoved;

    // "private set" = anyone can READ the name, but only code inside THIS class
    // can change it. Outside code must go through the Rename() method below.
    public string Name { get; private set; }

    public DeviceType DeviceType { get; }

    public DeviceOperationalState OperationalState { get; private set; }

    // Exposes the interfaces list as READ-ONLY to the outside world.
    // Outside code can look at it and loop over it, but cannot add/remove/replace
    // items in it - that would break the "single source of truth" rule.
    public IReadOnlyCollection<NetworkInterface> Interfaces => _interfaces.AsReadOnly();

    public void Rename(string name)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
    }

    public void PowerOn()
    {
        OperationalState = DeviceOperationalState.PoweredOn;
    }

    public void PowerOff()
    {
        OperationalState = DeviceOperationalState.PoweredOff;
    }

    // The ONLY way to give this device a new interface. This is what guarantees
    // "an interface always belongs to exactly one device": the interface object
    // is created right here, passing "this" (the current device) as its owner.
    public NetworkInterface AddInterface(string name, InterfaceType interfaceType)
    {
        var validatedName = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

        // Domain rule: two interfaces on the same device can't share a name
        // (e.g. can't have two "G0/0" on the same router).
        if (_interfaces.Any(i => i.Name.Equals(validatedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException($"Device '{Name}' already has an interface named '{validatedName}'.");
        }

        var networkInterface = new NetworkInterface(validatedName, interfaceType, this);
        _interfaces.Add(networkInterface);
        InterfaceAdded?.Invoke(this, networkInterface);
        return networkInterface;
    }

    public bool RemoveInterface(NetworkInterface networkInterface)
    {
        ArgumentNullException.ThrowIfNull(networkInterface);

        // Domain rule: can't just yank out an interface that's still plugged into
        // a cable. The connection has to be removed first (see Connection.cs).
        if (networkInterface.IsConnected)
        {
            throw new DomainException(
                $"Interface '{networkInterface.Name}' on device '{Name}' is still connected; remove the connection first.");
        }

        var removed = _interfaces.Remove(networkInterface);
        if (removed)
        {
            InterfaceRemoved?.Invoke(this, networkInterface);
        }

        return removed;
    }
}
