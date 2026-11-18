using NetSim.Core.Devices;   // brings in the "Devices" part of Core, so this file can use DeviceType (PC, Switch, Router...) and DeviceTypeRegistry (their screen names)

namespace NetSim.Core.Networking;   // the "address" of this file: it belongs to the Networking part of Core. Other files reach it with "using NetSim.Core.Networking"

// The rules of cable kinds (step 7) - the ONE place in the whole project that knows:
//   1. which PORTS can take which cable kind (a Serial cable only into a Serial port...);
//   2. which COPPER kind is right between two devices (straight-through or crossover);
//   3. whether a cable that was plugged in will actually WORK (its link is up) or not;
//   4. which cable "Automatic" picks.
// A cable that does not fit a port can't be plugged in at all (Connection.Create refuses it).
// A copper cable of the wrong kind DOES fit (both are Ethernet plugs) - but, like in Packet Tracer,
// its link stays down. Step 8 shows that as a red light on the cable.
// "public" = every part of the project may use it.
// "static" = there are no CableRules objects; the rules have nothing to remember, they only answer
// questions, so you call them straight through the class name: CableRules.CanPlug(...)
public static class CableRules
{
    // Purpose:       Tells which "plug" (port option) a cable kind needs in order to go into a port.
    // Precondition:  receives cableType - the kind of cable (straight-through, crossover, fiber, serial, wireless).
    // Postcondition: returns an InterfaceCapability - the option a port must have to take this cable
    //                (Ethernet for both copper kinds, Fiber, Serial, Wireless; None for an unknown kind).
    // "=> cableType switch { ... }" = the whole function is one "switch expression": it looks at the value of
    // cableType and returns the result written next to the matching line
    public static InterfaceCapability RequiredCapability(ConnectionType cableType) => cableType switch
    {
        ConnectionType.CopperStraightThrough => InterfaceCapability.Ethernet,   // a straight-through copper cable has a normal Ethernet (RJ-45) plug
        ConnectionType.CopperCrossover => InterfaceCapability.Ethernet,         // a crossover cable has the SAME Ethernet plug - only the wires inside are crossed, so it needs the same port option
        ConnectionType.Fiber => InterfaceCapability.Fiber,                      // a fiber cable has a fiber-optic plug - only ports with the Fiber option take it
        ConnectionType.Serial => InterfaceCapability.Serial,                    // a serial cable has a serial plug - only Serial ports take it
        ConnectionType.Wireless => InterfaceCapability.Wireless,                // wireless has no plug at all - it needs a port with a radio (Wireless option)
        _ => InterfaceCapability.None,                                          // "_" = any other value (a kind added later and forgotten here): None = no port takes it - safer than crashing
    };

    // Purpose:       Answers whether one port can physically take a cable of a given kind.
    // Precondition:  receives cableType - the kind of cable; port - the port to check (must not be null);
    //                reason - an "out" box the function fills in for the caller.
    // Postcondition: returns true when the cable's plug fits the port, otherwise false.
    //                Also fills reason: null when it fits, or a sentence explaining why not,
    //                e.g. "Port GigabitEthernet0/0 on Router0 can't take a Serial cable."
    // "bool" = the answer is true / false.
    // "out string? reason" = a second answer: the function MUST put a value into it before it ends, and the
    // caller gets that value back. "string?" = text that may be null (no reason when it fits)
    public static bool CanPlug(ConnectionType cableType, NetworkInterface port, out string? reason)
    {
        // If no port was passed in (null), stop right away with a clear error - that is a bug in the caller,
        // and it is better to find it here than to get a wrong answer
        ArgumentNullException.ThrowIfNull(port);

        // The heart of the check, in three parts:
        //   port.Capabilities             = the plugs this port takes (e.g. a Gigabit port: Ethernet | Fiber = 01001)
        //   RequiredCapability(cableType) = the plug this cable needs (e.g. Fiber = 01000)
        //   "&"                           = keeps only the bits that are "on" in BOTH (01001 & 01000 = 01000)
        //   "!= InterfaceCapability.None" = is anything left? something left = they share the plug = it fits
        // The true / false result is kept in the variable "fits"
        var fits = (port.Capabilities & RequiredCapability(cableType)) != InterfaceCapability.None;

        // Fill the "out" reason. "a ? b : c" = "if a then b, otherwise c" in one line:
        //   it fits      -> null (no reason needed);
        //   it does not  -> a sentence. "$" before the quotes lets us put values inside {...}:
        //                   the port's name, its device's name, and the cable's screen name
        reason = fits ? null : $"Port {port.Name} on {port.Device.Name} can't take a {DisplayName(cableType)} cable.";

        // Hand back the main answer: true = fits, false = does not fit
        return fits;
    }

    // Purpose:       Tells which COPPER cable kind is the right one between two kinds of device.
    // Precondition:  receives a - the kind of the first device; b - the kind of the second device
    //                (PC, Laptop, Server, Router or Switch).
    // Postcondition: returns CopperCrossover when both devices are on "the same side" (both switches, or both
    //                not switches), otherwise CopperStraightThrough.
    // Why only "is it a switch?": inside a copper cable there are two paths of wires. A PC, laptop, server and
    // router all SEND on path A and LISTEN on path B. A switch is built the other way round: it LISTENS on
    // path A and SENDS on path B. So there are only two "sides": switches, and everything else.
    //   different sides (PC - switch): sender meets listener with straight wires -> straight-through;
    //   the same side (PC - PC, router - PC!): sender would meet sender -> the wires must be crossed -> crossover.
    // Checking "same device type?" would be WRONG: a router and a PC are different types, but both are on the
    // same side, so they need a crossover cable.
    public static ConnectionType RequiredCopperType(DeviceType a, DeviceType b)
    {
        // Is the first device a switch? "==" compares (it does not assign); the answer, true or false, is kept in aIsSwitch.
        // true = it is on the "switch side"; false = it is on the "PC / router side"
        var aIsSwitch = a == DeviceType.Switch;

        // The same question for the second device, kept in bIsSwitch
        var bIsSwitch = b == DeviceType.Switch;

        // Are both answers the same? (both true = two switches; both false = e.g. two PCs or a router and a PC)
        //   the same answers  -> the same side      -> CopperCrossover
        //   different answers -> different sides    -> CopperStraightThrough
        // "a ? b : c" = if / otherwise in one line; "return" hands the chosen kind back to the caller
        return aIsSwitch == bIsSwitch ? ConnectionType.CopperCrossover : ConnectionType.CopperStraightThrough;
    }

    // Purpose:       Answers whether a cable of a given kind, between two given ports, will WORK (its link will be up).
    // Precondition:  receives cableType - the kind of cable that was chosen; a, b - the two ports at its ends
    //                (must not be null, and the cable must already fit both of them - CanPlug checks that);
    //                reason - an "out" box the function fills in for the caller.
    // Postcondition: returns true when the link will be up, otherwise false.
    //                Also fills reason: null when it works, or a sentence saying which cable was needed,
    //                e.g. "PC to Switch needs a Copper Straight-Through cable."
    //                Only copper can be "the wrong kind"; fiber and serial always work when they fit.
    public static bool WillLinkWork(ConnectionType cableType, NetworkInterface a, NetworkInterface b, out string? reason)
    {
        // If the first port is missing (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(a);

        // The same for the second port
        ArgumentNullException.ThrowIfNull(b);

        // Start with "no problem". An "out" parameter must get a value on EVERY way out of the function,
        // so setting it here first makes sure the two "return true" lines below leave it as null
        reason = null;

        // STEP 1 - is this a copper cable at all?
        // "is not ( X or Y )" = "is the value NEITHER X NOR Y?".
        // If the cable is neither straight-through nor crossover (it is fiber, serial or wireless), there is no
        // "wrong kind" for it - it works whenever it fits - so the answer is "yes" right away.
        // הכבל לא ישר וגם הוא לא מוצלב? כן עובד
        if (cableType is not (ConnectionType.CopperStraightThrough or ConnectionType.CopperCrossover))
        {
            return true;   // not copper -> the link works (reason stays null)
        }

        // STEP 2 - which copper kind do these two devices need?
        // a.Device = the device the first port belongs to; .DeviceType = its kind (PC, Switch...).
        // The same for b. RequiredCopperType (above) answers straight-through or crossover; the answer is kept in "required"
        var required = RequiredCopperType(a.Device.DeviceType, b.Device.DeviceType);

        // STEP 3 - is the cable that was chosen the one that is needed?
        if (cableType == required)
        {
            return true;   // the right kind -> the link works (reason stays null)
        }

        // If we got here, the cable is copper but the WRONG kind: it is plugged in, but it won't pass anything.
        // Build the explanation. DeviceTypeRegistry (step 1) knows each kind's screen name - "PC", "Switch" -
        // which reads better than the enum's own name
        var nameA = DeviceTypeRegistry.Get(a.Device.DeviceType).DisplayName;   // the first device kind's screen name, e.g. "PC"
        var nameB = DeviceTypeRegistry.Get(b.Device.DeviceType).DisplayName;   // the second device kind's screen name, e.g. "Switch"

        // Put the sentence into the "out" reason, e.g. "PC to Switch needs a Copper Straight-Through cable."
        // DisplayName(required) = the needed cable's screen name (the function at the bottom of this file)
        reason = $"{nameA} to {nameB} needs a {DisplayName(required)} cable.";

        // The main answer: the link will NOT work
        return false;
    }

    // Purpose:       Picks the cable that "Automatic" uses between two ports - one that fits both AND works.
    // Precondition:  receives a, b - the two ports to connect (must not be null).
    // Postcondition: returns the chosen cable kind (Serial, the right copper kind, or Fiber),
    //                or null when no cable kind fits both ports (e.g. a Serial port and an Ethernet port).
    // "ConnectionType?" = a cable kind, OR null ("?" makes room for "nothing found").
    // The order matters: Serial first (two serial ports should get a serial cable), then copper, then fiber.
    public static ConnectionType? ChooseAutomatic(NetworkInterface a, NetworkInterface b)
    {
        // If the first port is missing (null), stop right away with a clear error
        ArgumentNullException.ThrowIfNull(a);

        // The same for the second port
        ArgumentNullException.ThrowIfNull(b);

        // Try 1 - a serial cable: does it fit the first port AND ("&&") the second port?
        // "out _" = CanPlug gives a reason too, but we don't need it here - "_" throws it away
        if (CanPlug(ConnectionType.Serial, a, out _) && CanPlug(ConnectionType.Serial, b, out _))
        {
            return ConnectionType.Serial;   // two serial ports -> a serial cable
        }

        // Try 2 - copper. First find the RIGHT copper kind for these two devices (straight-through or crossover),
        // so "Automatic" never picks a copper cable whose link would be down
        var copper = RequiredCopperType(a.Device.DeviceType, b.Device.DeviceType);

        // Does that copper cable fit both ports? (both must have the Ethernet option)
        if (CanPlug(copper, a, out _) && CanPlug(copper, b, out _))
        {
            return copper;   // two Ethernet ports -> the right copper cable
        }

        // Try 3 - a fiber cable: does it fit both ports? (only Gigabit ports have the Fiber option)
        if (CanPlug(ConnectionType.Fiber, a, out _) && CanPlug(ConnectionType.Fiber, b, out _))
        {
            return ConnectionType.Fiber;   // two fiber ports -> fiber
        }

        // None of the three fits both ports -> no cable can join them
        return null;
    }

    // Purpose:       Gives a cable kind's name for the screen and for messages.
    // Precondition:  receives cableType - the kind of cable.
    // Postcondition: returns its name as text, e.g. "Copper Straight-Through".
    // Again a "switch expression": look at cableType and return the text next to the matching line
    public static string DisplayName(ConnectionType cableType) => cableType switch
    {
        ConnectionType.CopperStraightThrough => "Copper Straight-Through",   // the name Packet Tracer uses for a straight copper cable
        ConnectionType.CopperCrossover => "Copper Cross-Over",               // the name Packet Tracer uses for a crossover copper cable
        ConnectionType.Fiber => "Fiber",                                     // fiber optic
        ConnectionType.Serial => "Serial",                                   // serial line
        ConnectionType.Wireless => "Wireless",                               // wireless
        _ => cableType.ToString(),                                           // any other value: use the enum value's own name as text (ToString)
    };
}
