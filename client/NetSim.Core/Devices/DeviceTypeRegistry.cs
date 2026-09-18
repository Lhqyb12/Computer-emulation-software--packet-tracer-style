namespace NetSim.Core.Devices;

/// <summary>
/// The catalog of device types the domain currently knows how to construct (via
/// <see cref="NetworkDeviceFactory"/>). This is what a future Device Library screen (Phase 11)
/// enumerates to know which devices exist - it is not a UI component itself and carries no
/// Avalonia/UI dependency.
/// </summary>
public static class DeviceTypeRegistry
{
    private static readonly IReadOnlyList<DeviceTypeDescriptor> Descriptors =
    [
        new DeviceTypeDescriptor(DeviceType.Router, "Router"),
        new DeviceTypeDescriptor(DeviceType.Switch, "Switch"),
        new DeviceTypeDescriptor(DeviceType.Pc, "PC"),
        new DeviceTypeDescriptor(DeviceType.Server, "Server"),
        new DeviceTypeDescriptor(DeviceType.Laptop, "Laptop"),
    ];

    /// <summary>Every device type currently supported by <see cref="NetworkDeviceFactory"/>.</summary>
    public static IReadOnlyList<DeviceTypeDescriptor> SupportedTypes => Descriptors;

    public static DeviceTypeDescriptor Get(DeviceType deviceType)
    {
        foreach (var descriptor in Descriptors)
        {
            if (descriptor.DeviceType == deviceType)
            {
                return descriptor;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(deviceType), deviceType, "Unsupported device type.");
    }
}
