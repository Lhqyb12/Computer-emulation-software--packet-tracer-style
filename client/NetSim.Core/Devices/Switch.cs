using NetSim.Core.Common;   // EntityId

namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// A switch - connects devices inside one local network.
// Its MAC address table and VLANs are added later (Part 4).
// ": NetworkDevice" = it is a network device (not an end device - it passes traffic between others)
public class Switch : NetworkDevice
{
    // Purpose: Builds a new Switch with a new id.
    // Input:   name - the device name.
    // Output:  none (a constructor). Passes the name and DeviceType.Switch on to NetworkDevice
    public Switch(string name)
        : base(name, DeviceType.Switch)
    {
    }

    // Purpose: Builds a Switch with a saved id - only for loading a saved network.
    // Input:   id - the saved id; name - the device name.
    // Output:  none (a constructor). "internal" = only code inside NetSim.Core (the device factory) can call it
    internal Switch(EntityId id, string name)
        : base(id, name, DeviceType.Switch)
    {
    }
}
