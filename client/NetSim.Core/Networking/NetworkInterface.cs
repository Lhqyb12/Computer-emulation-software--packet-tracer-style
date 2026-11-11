using NetSim.Core.Common;              // EntityId
using NetSim.Core.Common.Exceptions;   // DomainException
using NetSim.Core.Devices;             // NetworkDevice

namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// One network port of a device (e.g. "FastEthernet0/1" on a switch).
// Always belongs to exactly one device - it is created only by NetworkDevice.AddInterface.
// Holds the port's kind, speed, cable options, MAC address, on/off, up/down, and the cable plugged into it.
// "sealed" = no other class can be built on this one
public sealed class NetworkInterface
{
    // Purpose: Builds a new port.
    // Input:   name - the port name; interfaceType - its kind; device - the device it belongs to.
    // Output:  none (a constructor). "internal" = only code inside NetSim.Core can call it -
    //          in practice only NetworkDevice.AddInterface, so a port always has an owner.
    internal NetworkInterface(string name, InterfaceType interfaceType, NetworkDevice device)
    {
        Id = EntityId.New();                                           // a new unique id
        Name = name;                                                   // the port name
        InterfaceType = interfaceType;                                 // the port kind
        Device = device;                                               // the owning device
        Speed = InterfaceTypeInfo.DefaultSpeed(interfaceType);         // the default speed of this kind
        Capabilities = InterfaceTypeInfo.Capabilities(interfaceType);  // which cables this kind takes
        AdministrativeState = InterfaceAdministrativeState.Enabled;    // a new port starts turned on
        OperationalState = InterfaceOperationalState.Down;             // and not working (no cable yet)

        // Only ports that take an Ethernet cable get a MAC address (Serial / Console don't have one).
        // "HasFlag" = is this option inside the combined value
        if (Capabilities.HasFlag(InterfaceCapability.Ethernet))
        {
            // A random address, made once - it never changes after that.
            // The full name is written because this class also has a property called MacAddress
            MacAddress = NetSim.Core.Networking.MacAddress.CreateRandomUnicast();
        }
    }

    // The port's unique id
    public EntityId Id { get; }

    // The port name, unique inside its device (e.g. "GigabitEthernet0/0"). Never changes
    public string Name { get; }

    // The port kind
    public InterfaceType InterfaceType { get; }

    // The device this port belongs to. Only "get" - a port can never move to another device
    public NetworkDevice Device { get; }

    // The port speed. "private set" = only this class can change it (through SetSpeed)
    public InterfaceSpeed Speed { get; private set; }

    // Which kinds of cable this port can take
    public InterfaceCapability Capabilities { get; }

    // True when this port takes an Ethernet cable (and so has a MAC address)
    public bool SupportsEthernet => Capabilities.HasFlag(InterfaceCapability.Ethernet);

    // The MAC address, or null for a port without one (Serial, Console). "?" = may be null
    public MacAddress? MacAddress { get; private set; }

    // Did the user turn the port on or off
    public InterfaceAdministrativeState AdministrativeState { get; private set; }

    // Is the port really working
    public InterfaceOperationalState OperationalState { get; private set; }

    // An optional label the user can give the port (e.g. "To the main switch"). Not an id
    public string? Description { get; private set; }

    // The short name for the canvas, e.g. "GigabitEthernet0/0" -> "G0/0"
    public string ShortName => InterfaceTypeInfo.ShortName(InterfaceType, Name);

    // What to show as the port's main label: the description when there is one, otherwise the name.
    // "!" = we know Description is not null here
    public string DisplayName => string.IsNullOrWhiteSpace(Description) ? Name : Description!;

    // The cable plugged into this port, or null when nothing is plugged in. Every port starts empty
    public Connection? Connection { get; private set; }

    // True when a cable is plugged in
    public bool IsConnected => Connection is not null;

    // True when the user turned the port on
    public bool IsEnabled => AdministrativeState == InterfaceAdministrativeState.Enabled;

    // True when a new cable can be plugged in: turned on, and nothing plugged in yet
    public bool IsAvailable => IsEnabled && !IsConnected;

    // True when the port really works: turned on AND up
    public bool IsOperational => IsEnabled && OperationalState == InterfaceOperationalState.Up;

    // Purpose: Sets the port's label.
    // Input:   description - the label; empty or null removes it.
    // Output:  nothing.
    public void SetDescription(string? description) =>
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();   // empty -> null, otherwise cleaned

    // Purpose: Changes the port speed (e.g. when loading a saved network).
    // Input:   speed - the new speed.
    // Output:  nothing.
    public void SetSpeed(InterfaceSpeed speed) => Speed = speed;

    // Purpose: Puts back a saved MAC address - only when loading a saved network,
    //          so the port keeps the same address it had, instead of a new random one.
    // Input:   macAddress - the saved address.
    // Output:  nothing.
    public void SetMacAddress(MacAddress macAddress) => MacAddress = macAddress;

    // Purpose: Turns the port on (later in the CLI: "no shutdown").
    // Input:   nothing.
    // Output:  nothing. It does not make the port "up" by itself - that needs a working cable.
    public void Enable() => AdministrativeState = InterfaceAdministrativeState.Enabled;

    // Purpose: Turns the port off (later in the CLI: "shutdown").
    // Input:   nothing.
    // Output:  nothing. The port also goes down; a plugged-in cable stays plugged in, it just stops working.
    public void Disable()
    {
        AdministrativeState = InterfaceAdministrativeState.Disabled;   // turned off by the user
        OperationalState = InterfaceOperationalState.Down;             // so it can't be working
    }

    // Purpose: Marks the port as working.
    // Input:   nothing.
    // Output:  nothing. Throws DomainException when the port is turned off.
    public void BringUp()
    {
        // A turned-off port can't work
        if (!IsEnabled)
        {
            throw new DomainException(
                $"Interface '{Name}' on device '{Device.Name}' is administratively disabled and cannot be brought up.");
        }

        // Working
        OperationalState = InterfaceOperationalState.Up;
    }

    // Purpose: Marks the port as not working.
    // Input:   nothing.
    // Output:  nothing.
    public void BringDown()
    {
        OperationalState = InterfaceOperationalState.Down;
    }

    // Purpose: Plugs a cable into this port. "internal" = only Connection.Create calls it,
    //          so a cable can't be half-plugged from outside.
    // Input:   connection - the cable.
    // Output:  nothing. Throws DomainException when a cable is already plugged in.
    internal void AttachConnection(Connection connection)
    {
        // Only one cable per port
        if (Connection is not null)
        {
            throw new DomainException($"Interface '{Name}' on device '{Device.Name}' is already connected.");
        }

        // Keep the cable
        Connection = connection;
    }

    // Purpose: Unplugs the cable from this port. Only Connection.Disconnect calls it.
    // Input:   nothing.
    // Output:  nothing.
    internal void DetachConnection()
    {
        Connection = null;   // nothing plugged in now
    }
}
