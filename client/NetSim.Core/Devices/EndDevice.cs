using NetSim.Core.Common;   // EntityId

namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The base of the "end" devices - the ones at the edge of the network that use it (PC, laptop, server),
// as opposed to devices that pass traffic between others (router, switch).
// "abstract" = nobody builds a plain EndDevice; ": NetworkDevice" = it is a network device
public abstract class EndDevice : NetworkDevice
{
    // Purpose: Builds a new end device with a new id.
    // Input:   name - the device name; deviceType - Pc, Laptop or Server.
    // Output:  none (a constructor). ": base(...)" = pass both on to NetworkDevice
    protected EndDevice(string name, DeviceType deviceType)
        : base(name, deviceType)
    {
    }

    // Purpose: Builds an end device with a saved id - only for loading a saved network.
    // Input:   id - the saved id; name - the device name; deviceType - its kind.
    // Output:  none (a constructor).
    protected EndDevice(EntityId id, string name, DeviceType deviceType)
        : base(id, name, deviceType)
    {
    }
}
