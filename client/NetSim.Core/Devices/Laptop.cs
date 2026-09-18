using NetSim.Core.Common;

namespace NetSim.Core.Devices;

/// <summary>
/// A representative end-device type, distinct from <see cref="Pc"/> for the sake of the
/// device library the UI will eventually offer. IP configuration and application-level
/// behavior are intentionally not implemented yet.
/// </summary>
public class Laptop : EndDevice
{
    public Laptop(string name)
        : base(name, DeviceType.Laptop)
    {
    }

    // Persistence rehydration only - see NetworkDeviceFactory.RehydrateWithoutDefaults.
    internal Laptop(EntityId id, string name)
        : base(id, name, DeviceType.Laptop)
    {
    }
}
