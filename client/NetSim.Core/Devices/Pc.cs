using NetSim.Core.Common;   // EntityId

namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// A desktop computer.
// ": EndDevice" = it is an end device
public class Pc : EndDevice
{
    // Purpose: Builds a new Pc with a new id.
    // Input:   name - the device name.
    // Output:  none (a constructor). Passes the name and DeviceType.Pc on to EndDevice
    public Pc(string name)
        : base(name, DeviceType.Pc)
    {
    }

    // Purpose: Builds a Pc with a saved id - only for loading a saved network.
    // Input:   id - the saved id; name - the device name.
    // Output:  none (a constructor). "internal" = only code inside NetSim.Core (the device factory) can call it
    internal Pc(EntityId id, string name)
        : base(id, name, DeviceType.Pc)
    {
    }
}
