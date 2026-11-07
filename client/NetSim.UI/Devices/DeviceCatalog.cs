using System.Collections.Generic;   // IReadOnlyList
using System.Linq;                  // Select, ToList
using Avalonia.Controls;            // TryFindResource
using Avalonia.Media;               // StreamGeometry
using NetSim.Core.Devices;          // DeviceTypeRegistry, DeviceType

namespace NetSim.UI.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Builds the Device Library's cards: one card for every device kind in DeviceTypeRegistry (Core),
// with the screen-only details (group, description, icon) added.
// The cards are built in code, not written by hand in the XAML - so a new device kind needs no change in the view
public static class DeviceCatalog
{
    // All the cards, built once, the first time someone reads this
    public static IReadOnlyList<DeviceLibraryEntry> AllDevices { get; } = BuildEntries();

    // Purpose: Builds one card for each supported device kind.
    // Input:   nothing (reads DeviceTypeRegistry.SupportedTypes).
    // Output:  the list of cards.
    // "Select" = turn every item of a list into something else; "ToList" = collect the results into a list
    private static IReadOnlyList<DeviceLibraryEntry> BuildEntries() =>
        DeviceTypeRegistry.SupportedTypes
            .Select(descriptor => new DeviceLibraryEntry(
                descriptor.DeviceType,                                     // the kind - from Core
                descriptor.DisplayName,                                    // the name - from Core
                CategoryFor(descriptor.DeviceType),                        // the group - decided below
                DescriptionFor(descriptor.DeviceType),                     // the sentence - decided below
                ResolveIcon(DeviceIcons.ResourceKeyFor(descriptor.DeviceType))))   // the icon - found by its name
            .ToList();

    // Purpose: Decides which group a device kind belongs to.
    // Input:   deviceType - the device kind.
    // Output:  its group.
    // "or" = either of these values
    private static DeviceCategory CategoryFor(DeviceType deviceType) => deviceType switch
    {
        DeviceType.Router or DeviceType.Switch => DeviceCategory.NetworkDevices,               // devices that move traffic
        DeviceType.Pc or DeviceType.Laptop or DeviceType.Server => DeviceCategory.EndDevices,  // devices a person uses
        _ => DeviceCategory.Other,                                                             // anything else
    };

    // Purpose: Gives the short sentence shown under a device's name.
    // Input:   deviceType - the device kind.
    // Output:  the sentence.
    private static string DescriptionFor(DeviceType deviceType) => deviceType switch
    {
        DeviceType.Router => "Routes packets between networks.",                     // router
        DeviceType.Switch => "Connects devices within a local network.",             // switch
        DeviceType.Pc => "A desktop endpoint for configuration and testing.",        // PC
        DeviceType.Laptop => "A portable endpoint for configuration and testing.",   // laptop
        DeviceType.Server => "Hosts services for other devices on the network.",     // server
        _ => "A network device.",                                                    // anything else
    };

    // Purpose: Finds an icon shape by its resource name (from Theme/Icons.axaml).
    // Input:   resourceKey - e.g. "Icon.Router".
    // Output:  the shape, or null if it was not found.
    // "global::Avalonia.Application" - the full name on purpose: inside NetSim.UI, the word
    // "Application" alone would mean our own NetSim.Application project, not Avalonia's class.
    // "Current?." = only if the app is running (in tests it is not, and then the icon is just null)
    private static StreamGeometry? ResolveIcon(string resourceKey) =>
        global::Avalonia.Application.Current?.TryFindResource(resourceKey, out var resource) == true
            ? resource as StreamGeometry   // found - give it back as an icon shape
            : null;                        // not found - no icon
}
