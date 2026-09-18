using NetSim.Core.Common;

namespace NetSim.Core.Devices;

/// <summary>
/// Common abstraction for endpoint devices (PCs, servers, and similar) that sit at the
/// edge of the network rather than forwarding traffic between other devices.
/// </summary>
public abstract class EndDevice : NetworkDevice
{
    protected EndDevice(string name, DeviceType deviceType)
        : base(name, deviceType)
    {
    }

    // See NetworkDevice's matching (EntityId, string, DeviceType) overload - persistence
    // rehydration only.
    protected EndDevice(EntityId id, string name, DeviceType deviceType)
        : base(id, name, deviceType)
    {
    }
}
