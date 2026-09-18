using NetSim.Core.Devices;

namespace NetSim.UI.Devices;

/// <summary>
/// The single place that maps a <see cref="DeviceType"/> to the <c>Theme/Icons.axaml</c> resource
/// key that draws it - both the Device Library cards (<see cref="DeviceCatalog"/>) and the Network
/// Canvas's own device rendering (<c>NetworkCanvasControl</c>) go through this instead of each
/// hard-coding their own DeviceType-to-icon switch, so adding a new device type's icon is a single
/// line here rather than an edit in two places.
/// </summary>
public static class DeviceIcons
{
    public static string ResourceKeyFor(DeviceType deviceType) => deviceType switch
    {
        DeviceType.Router => "Icon.Router",
        DeviceType.Switch => "Icon.Switch",
        DeviceType.Pc => "Icon.Pc",
        DeviceType.Laptop => "Icon.Laptop",
        DeviceType.Server => "Icon.Server",
        _ => "Icon.Network",
    };
}
