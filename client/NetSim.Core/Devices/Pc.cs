using NetSim.Core.Common;

namespace NetSim.Core.Devices;

/// <summary>
/// A representative end-device type. IP configuration and application-level behavior
/// are intentionally not implemented yet.
/// </summary>
public class Pc : EndDevice
{
    public Pc(string name)
        : base(name, DeviceType.Pc)
    {
    }

    // Persistence rehydration only - see NetworkDeviceFactory.RehydrateWithoutDefaults.
    internal Pc(EntityId id, string name)
        : base(id, name, DeviceType.Pc)
    {
    }
}
