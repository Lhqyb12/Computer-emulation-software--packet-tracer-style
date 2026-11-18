using NetSim.Core.Devices;   // DeviceType - which kind of device is at each end

namespace NetSim.Core.Networking;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The rules of cable kinds (step 7) - the one place that knows:
//   1. which PORTS can take which cable kind (a Serial cable only into a Serial port...);
//   2. which COPPER kind is right between two devices (straight-through or crossover);
//   3. whether a cable that was plugged in will actually WORK (its link is up) or not;
//   4. which cable "Automatic" picks.
// A cable that does not fit a port can't be plugged in at all (Connection.Create refuses it).
// A copper cable of the wrong kind DOES fit (both are Ethernet plugs) - but, like in Packet Tracer,
// its link stays down. Step 8 shows that as a red light on the cable.
// "static class" = no objects, just call CableRules.Something(...)
public static class CableRules
{
    // Purpose:       Gives the port option a cable kind needs (its "plug").
    // Precondition:  none.
    // Postcondition: returns the InterfaceCapability the port must have to take this cable. Nothing is changed.
    public static InterfaceCapability RequiredCapability(ConnectionType cableType) => cableType switch
    {
        ConnectionType.CopperStraightThrough => InterfaceCapability.Ethernet,   // a copper Ethernet plug
        ConnectionType.CopperCrossover => InterfaceCapability.Ethernet,         // the same plug, crossed wires inside
        ConnectionType.Fiber => InterfaceCapability.Fiber,                      // a fiber plug
        ConnectionType.Serial => InterfaceCapability.Serial,                    // a serial plug
        ConnectionType.Wireless => InterfaceCapability.Wireless,                // no plug - a radio
        _ => InterfaceCapability.None,                                          // anything else - nothing fits
    };

    // Purpose:       Answers whether one port can physically take a cable kind.
    // Precondition:  port is not null.
    // Postcondition: true when the port's options include the cable's plug; otherwise false + a reason
    //                (e.g. "Port GigabitEthernet0/0 can't take a Serial cable."). Nothing is changed.
    public static bool CanPlug(ConnectionType cableType, NetworkInterface port, out string? reason)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(port);

        // The plug this cable needs, and does the port have it? ("&" = the options both have; None = nothing shared)
        var fits = (port.Capabilities & RequiredCapability(cableType)) != InterfaceCapability.None;

        // A reason only when it does not fit ("? :" = if / else in one line)
        reason = fits ? null : $"Port {port.Name} on {port.Device.Name} can't take a {DisplayName(cableType)} cable.";

        return fits;   // fits or not
    }

    // Purpose:       Says which COPPER kind is right between two kinds of device.
    // Precondition:  none.
    // Postcondition: CopperCrossover when both are "the same side" (both switches, or both not switches),
    //                otherwise CopperStraightThrough. Nothing is changed.
    // Why: inside a copper cable, a computer / router SENDS on the wires a switch RECEIVES on. Two
    // different sides line up with straight wires; two of the same side need the wires crossed.
    public static ConnectionType RequiredCopperType(DeviceType a, DeviceType b)
    {
        // Is each end a switch? (PC, laptop, server and router all sit on the other side)
        var aIsSwitch = a == DeviceType.Switch;
        var bIsSwitch = b == DeviceType.Switch;

        // The same side -> crossover; different sides -> straight-through
        return aIsSwitch == bIsSwitch ? ConnectionType.CopperCrossover : ConnectionType.CopperStraightThrough;
    }

    // Purpose:       Answers whether a cable of this kind between these two ports will WORK (link up).
    // Precondition:  a and b are not null, and the cable fits both ports (CanPlug).
    // Postcondition: true when the link will be up; otherwise false + a reason, e.g.
    //                "PC to Switch needs a Copper Straight-Through cable." Nothing is changed.
    //                Only copper can be "the wrong kind"; fiber and serial always work when they fit.
    public static bool WillLinkWork(ConnectionType cableType, NetworkInterface a, NetworkInterface b, out string? reason)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        reason = null;   // no problem yet

        // Not a copper cable - nothing more to check
        if (cableType is not (ConnectionType.CopperStraightThrough or ConnectionType.CopperCrossover))
        {
            return true;   // works
        }

        // The copper kind these two devices need
        var required = RequiredCopperType(a.Device.DeviceType, b.Device.DeviceType);

        // The right kind - it works
        if (cableType == required)
        {
            return true;
        }

        // The wrong kind - it is plugged in, but it won't pass anything.
        // DeviceTypeRegistry gives the kinds' screen names ("PC", "Switch"...)
        var nameA = DeviceTypeRegistry.Get(a.Device.DeviceType).DisplayName;
        var nameB = DeviceTypeRegistry.Get(b.Device.DeviceType).DisplayName;
        reason = $"{nameA} to {nameB} needs a {DisplayName(required)} cable.";
        return false;
    }

    // Purpose:       Picks the cable "Automatic" uses between two ports.
    // Precondition:  a and b are not null.
    // Postcondition: returns the kind that fits both ports AND makes the link work, or null when no kind fits
    //                both (e.g. a Serial port and an Ethernet port). Nothing is changed.
    //                Order: Serial (two serial ports), then the right copper kind, then fiber.
    public static ConnectionType? ChooseAutomatic(NetworkInterface a, NetworkInterface b)
    {
        // null is not allowed - stop with a clear message
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        // Two serial ports - a serial cable
        if (CanPlug(ConnectionType.Serial, a, out _) && CanPlug(ConnectionType.Serial, b, out _))
        {
            return ConnectionType.Serial;
        }

        // Two Ethernet ports - the RIGHT copper kind for these two devices ("out _" = we don't need the reason)
        var copper = RequiredCopperType(a.Device.DeviceType, b.Device.DeviceType);
        if (CanPlug(copper, a, out _) && CanPlug(copper, b, out _))
        {
            return copper;
        }

        // Two fiber ports - fiber
        if (CanPlug(ConnectionType.Fiber, a, out _) && CanPlug(ConnectionType.Fiber, b, out _))
        {
            return ConnectionType.Fiber;
        }

        // Nothing fits both
        return null;
    }

    // Purpose:       Gives a cable kind's name for the screen and for messages.
    // Precondition:  none.
    // Postcondition: e.g. "Copper Straight-Through". Nothing is changed.
    public static string DisplayName(ConnectionType cableType) => cableType switch
    {
        ConnectionType.CopperStraightThrough => "Copper Straight-Through",
        ConnectionType.CopperCrossover => "Copper Cross-Over",
        ConnectionType.Fiber => "Fiber",
        ConnectionType.Serial => "Serial",
        ConnectionType.Wireless => "Wireless",
        _ => cableType.ToString(),   // anything else - its own name
    };
}
