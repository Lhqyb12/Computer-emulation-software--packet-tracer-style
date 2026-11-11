using NetSim.Core.Common;   // EntityId

namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// A router - connects different networks and passes packets between them.
// Its routing table is added later (Part 4, routing).
// ": NetworkDevice" = it is a network device (not an end device - it passes traffic between others)
public class Router : NetworkDevice
{
    // Purpose: Builds a new Router with a new id.
    // Input:   name - the device name.
    // Output:  none (a constructor). Passes the name and DeviceType.Router on to NetworkDevice
    public Router(string name)
        : base(name, DeviceType.Router)
    {
    }

    // Purpose: Builds a Router with a saved id - only for loading a saved network.
    // Input:   id - the saved id; name - the device name.
    // Output:  none (a constructor). "internal" = only code inside NetSim.Core (the device factory) can call it
    internal Router(EntityId id, string name)
        : base(id, name, DeviceType.Router)
    {
    }
}
