namespace NetSim.Core.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// The list of every device kind the simulator supports. The Device Library reads this list to
// know which cards to show. It is not a screen - it has no UI at all.
// "static" = no objects are created from it; it is used directly by its name
public static class DeviceTypeRegistry
{
    // The list itself, written once. "readonly" = it can't be replaced by another list.
    // "[ ... ]" = a short way to write a list of items
    private static readonly IReadOnlyList<DeviceTypeDescriptor> Descriptors =
    [
        new DeviceTypeDescriptor(DeviceType.Router, "Router"),   // a router, shown as "Router"
        new DeviceTypeDescriptor(DeviceType.Switch, "Switch"),   // a switch, shown as "Switch"
        new DeviceTypeDescriptor(DeviceType.Pc, "PC"),           // a desktop computer, shown as "PC"
        new DeviceTypeDescriptor(DeviceType.Server, "Server"),   // a server, shown as "Server"
        new DeviceTypeDescriptor(DeviceType.Laptop, "Laptop"),   // a laptop, shown as "Laptop"
    ];

    // Every supported device kind. "IReadOnlyList" = whoever reads it can't add or remove items
    public static IReadOnlyList<DeviceTypeDescriptor> SupportedTypes => Descriptors;

    // Purpose: Finds the details of one device kind.
    // Input:   deviceType - the kind to look for.
    // Output:  its descriptor; throws an exception if the kind is not in the list.
    public static DeviceTypeDescriptor Get(DeviceType deviceType)
    {
        // Go over the list one item at a time
        foreach (var descriptor in Descriptors)
        {
            // Found the kind we are looking for
            if (descriptor.DeviceType == deviceType)
            {
                return descriptor;   // give it back to the caller
            }
        }

        // Not found - this is a programming mistake, so stop with a clear message
        throw new ArgumentOutOfRangeException(nameof(deviceType), deviceType, "Unsupported device type.");
    }
}
