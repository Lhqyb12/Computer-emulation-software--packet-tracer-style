using NetSim.Core.Networking;   // InterfaceType

namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The one place that says which ports every kind of device starts with.
// The device factory (step 1.8) asks it for the list and makes a real port from every line.
// "static class" = no objects, just call DeviceInterfaceCatalog.For(...)
public static class DeviceInterfaceCatalog
{
    // How many ports a new switch has. Small on purpose - enough for a lab
    private const int DefaultSwitchPortCount = 8;

    // Purpose: The list of ports a new device of this kind starts with.
    // Input:   deviceType - the kind of device.
    // Output:  the list of port plans. Throws ArgumentOutOfRangeException for an unknown kind.
    //          "[ ... ]" = a ready-made list; "switch" = pick the part that matches the kind
    public static IReadOnlyList<InterfaceDefinition> For(DeviceType deviceType) => deviceType switch
    {
        // A router: two Gigabit ports and one serial port
        DeviceType.Router =>
        [
            new InterfaceDefinition("GigabitEthernet0/0", InterfaceType.GigabitEthernet),
            new InterfaceDefinition("GigabitEthernet0/1", InterfaceType.GigabitEthernet),
            new InterfaceDefinition("Serial0/0", InterfaceType.Serial),
        ],

        // A switch: 8 Fast Ethernet ports, built by the method below
        DeviceType.Switch => BuildSwitchPorts(),

        // A PC, laptop or server: one Ethernet port. "or" = any of these kinds
        DeviceType.Pc or DeviceType.Laptop or DeviceType.Server =>
        [
            new InterfaceDefinition("Ethernet0", InterfaceType.Ethernet),
        ],

        // Any other kind - not supported
        _ => throw new ArgumentOutOfRangeException(nameof(deviceType), deviceType, "Unsupported device type."),
    };

    // Purpose: Builds the port list of a switch: FastEthernet0/1 up to FastEthernet0/8.
    // Input:   nothing.
    // Output:  the list of 8 port plans.
    private static IReadOnlyList<InterfaceDefinition> BuildSwitchPorts()
    {
        // An empty list with room for 8
        var ports = new List<InterfaceDefinition>(DefaultSwitchPortCount);

        // Port numbers 1 to 8
        for (var portNumber = 1; portNumber <= DefaultSwitchPortCount; portNumber++)
        {
            // e.g. "FastEthernet0/3"
            ports.Add(new InterfaceDefinition($"FastEthernet0/{portNumber}", InterfaceType.FastEthernet));
        }

        // Hand the list back
        return ports;
    }
}
