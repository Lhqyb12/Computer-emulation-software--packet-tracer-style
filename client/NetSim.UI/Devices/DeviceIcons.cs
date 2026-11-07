using NetSim.Core.Devices;   // DeviceType

namespace NetSim.UI.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The one place that says which icon draws each device kind. Both the Device Library cards and,
// later, the canvas use it - so a new device kind's icon is added in one line here, not in two places
public static class DeviceIcons
{
    // Purpose: Gives the name of the icon resource (in Theme/Icons.axaml) for a device kind.
    // Input:   deviceType - the device kind.
    // Output:  the resource name, e.g. "Icon.Router".
    // "switch" with "=>" = pick one result by the value; "_" = any other value
    public static string ResourceKeyFor(DeviceType deviceType) => deviceType switch
    {
        DeviceType.Router => "Icon.Router",   // router -> the router drawing
        DeviceType.Switch => "Icon.Switch",   // switch -> the switch drawing
        DeviceType.Pc => "Icon.Pc",           // PC -> the computer drawing
        DeviceType.Laptop => "Icon.Laptop",   // laptop -> the laptop drawing
        DeviceType.Server => "Icon.Server",   // server -> the server drawing
        _ => "Icon.Network",                  // anything else -> the general network drawing
    };
}
