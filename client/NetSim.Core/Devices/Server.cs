using NetSim.Core.Common;

namespace NetSim.Core.Devices;

/// <summary>
/// A representative end-device type. Server-side services are intentionally not
/// implemented yet.
/// </summary>
public class Server : EndDevice
{
    public Server(string name)
        : base(name, DeviceType.Server)
    {
    }

    // Persistence rehydration only - see NetworkDeviceFactory.RehydrateWithoutDefaults.
    internal Server(EntityId id, string name)
        : base(id, name, DeviceType.Server)
    {
    }
}
