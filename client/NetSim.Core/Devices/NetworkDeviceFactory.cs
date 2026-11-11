using NetSim.Core.Common;   // EntityId

namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The one place that knows how to build every kind of device, with its starting ports.
// Other code asks "give me a Router called R1" here, instead of writing "new Router(...)" itself -
// so adding a new kind of device later means changing only this file (and the kind's own class).
// "static class" = no objects, just call NetworkDeviceFactory.Create(...)
public static class NetworkDeviceFactory
{
    // Purpose: Builds a brand-new device of the given kind, with all its starting ports.
    //          This is the normal way to make a device (e.g. when the user drops one on the canvas).
    // Input:   deviceType - the kind; name - the device name.
    // Output:  the new device, with its ports already added.
    public static NetworkDevice Create(DeviceType deviceType, string name)
    {
        // Build the device without ports
        var device = CreateWithoutDefaults(deviceType, name);

        // Add the starting ports of this kind
        ApplyDefaultInterfaces(device);

        // Hand the ready device back
        return device;
    }

    // Purpose: Builds a new device WITHOUT any ports.
    //          Only for loading a saved network, where the saved ports are added back one by one.
    // Input:   deviceType - the kind; name - the device name.
    // Output:  the new device, with no ports. Throws ArgumentOutOfRangeException for an unknown kind.
    //          "switch" = pick the line that matches the kind
    public static NetworkDevice CreateWithoutDefaults(DeviceType deviceType, string name) => deviceType switch
    {
        DeviceType.Router => new Router(name),     // a router
        DeviceType.Switch => new Switch(name),     // a switch
        DeviceType.Pc => new Pc(name),             // a PC
        DeviceType.Server => new Server(name),     // a server
        DeviceType.Laptop => new Laptop(name),     // a laptop
        _ => throw new ArgumentOutOfRangeException(nameof(deviceType), deviceType, "Unsupported device type."),   // anything else
    };

    // Purpose: Rebuilds a saved device with its saved id, WITHOUT any ports - only for loading a saved network.
    // Input:   id - the saved id; deviceType - the kind; name - the device name.
    // Output:  the device, with no ports. Throws ArgumentOutOfRangeException for an unknown kind.
    public static NetworkDevice RehydrateWithoutDefaults(EntityId id, DeviceType deviceType, string name) => deviceType switch
    {
        DeviceType.Router => new Router(id, name),     // a router
        DeviceType.Switch => new Switch(id, name),     // a switch
        DeviceType.Pc => new Pc(id, name),             // a PC
        DeviceType.Server => new Server(id, name),     // a server
        DeviceType.Laptop => new Laptop(id, name),     // a laptop
        _ => throw new ArgumentOutOfRangeException(nameof(deviceType), deviceType, "Unsupported device type."),   // anything else
    };

    // Purpose: Adds to a device the starting ports of its kind, as listed in DeviceInterfaceCatalog.
    // Input:   device - the device (still without ports).
    // Output:  nothing. The device now has its ports.
    private static void ApplyDefaultInterfaces(NetworkDevice device)
    {
        // Every port plan of this kind (e.g. 8 lines for a switch)
        foreach (var definition in DeviceInterfaceCatalog.For(device.DeviceType))
        {
            // Make a real port from the plan
            device.AddInterface(definition.Name, definition.InterfaceType);
        }
    }
}
