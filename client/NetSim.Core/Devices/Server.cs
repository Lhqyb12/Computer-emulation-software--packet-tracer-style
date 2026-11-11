using NetSim.Core.Common;   // EntityId

namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// A server - a computer that gives services to others (web, DHCP, DNS - later, in Part 4).
// ": EndDevice" = it is an end device
public class Server : EndDevice
{
    // Purpose: Builds a new Server with a new id.
    // Input:   name - the device name.
    // Output:  none (a constructor). Passes the name and DeviceType.Server on to EndDevice
    public Server(string name)
        : base(name, DeviceType.Server)
    {
    }

    // Purpose: Builds a Server with a saved id - only for loading a saved network.
    // Input:   id - the saved id; name - the device name.
    // Output:  none (a constructor). "internal" = only code inside NetSim.Core (the device factory) can call it
    internal Server(EntityId id, string name)
        : base(id, name, DeviceType.Server)
    {
    }
}
