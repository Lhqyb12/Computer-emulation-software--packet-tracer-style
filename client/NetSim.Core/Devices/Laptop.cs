using NetSim.Core.Common;   // EntityId

namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// A laptop - the same as a PC, kept as its own kind for the device library.
// ": EndDevice" = it is an end device
public class Laptop : EndDevice
{
    // Purpose: Builds a new Laptop with a new id.
    // Input:   name - the device name.
    // Output:  none (a constructor). Passes the name and DeviceType.Laptop on to EndDevice
    public Laptop(string name)
        : base(name, DeviceType.Laptop)
    {
    }

    // Purpose: Builds a Laptop with a saved id - only for loading a saved network.
    // Input:   id - the saved id; name - the device name.
    // Output:  none (a constructor). "internal" = only code inside NetSim.Core (the device factory) can call it
    internal Laptop(EntityId id, string name)
        : base(id, name, DeviceType.Laptop)
    {
    }
}
