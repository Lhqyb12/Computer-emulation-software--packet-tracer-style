using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using NetSim.Core.Devices;

namespace NetSim.UI.Devices;

/// <summary>
/// The Device Library's data source: every device type <c>NetworkDeviceFactory</c> can actually
/// build (via <see cref="DeviceTypeRegistry.SupportedTypes"/>), enriched with the UI-only display
/// data (<see cref="DeviceCategory"/>/description/icon) a card needs. Deliberately not hand-built
/// in XAML (see docs/architecture/device-model.md, "Visual identity", and the Phase 11 brief's
/// "do not hardcode the entire library directly inside XAML") - a future device type only needs a
/// <see cref="DeviceType"/> value + concrete class + factory case + registry entry (Core, already
/// established in Phase 10) plus one switch arm each in <see cref="CategoryFor"/>/
/// <see cref="DescriptionFor"/> here, never a change to <c>DeviceLibraryView.axaml</c> itself.
/// </summary>
public static class DeviceCatalog
{
    public static IReadOnlyList<DeviceLibraryEntry> AllDevices { get; } = BuildEntries();

    private static IReadOnlyList<DeviceLibraryEntry> BuildEntries() =>
        DeviceTypeRegistry.SupportedTypes
            .Select(descriptor => new DeviceLibraryEntry(
                descriptor.DeviceType,
                descriptor.DisplayName,
                CategoryFor(descriptor.DeviceType),
                DescriptionFor(descriptor.DeviceType),
                ResolveIcon(DeviceIcons.ResourceKeyFor(descriptor.DeviceType))))
            .ToList();

    private static DeviceCategory CategoryFor(DeviceType deviceType) => deviceType switch
    {
        DeviceType.Router or DeviceType.Switch => DeviceCategory.NetworkDevices,
        DeviceType.Pc or DeviceType.Laptop or DeviceType.Server => DeviceCategory.EndDevices,
        _ => DeviceCategory.Other,
    };

    private static string DescriptionFor(DeviceType deviceType) => deviceType switch
    {
        DeviceType.Router => "Routes packets between networks.",
        DeviceType.Switch => "Connects devices within a local network.",
        DeviceType.Pc => "A desktop endpoint for configuration and testing.",
        DeviceType.Laptop => "A portable endpoint for configuration and testing.",
        DeviceType.Server => "Hosts services for other devices on the network.",
        _ => "A network device.",
    };

    // Icon.* resources (Theme/Icons.axaml) are plain, theme-invariant StreamGeometry - not inside
    // a ThemeDictionary - so a one-time lookup here is safe (no Dark/Light re-resolution needed),
    // the same assumption NetworkWorkspaceView.axaml already makes with StaticResource for icons.
    // Avalonia.Application.Current is null in headless unit tests, so this degrades to a null Icon
    // there rather than throwing - callers must treat Icon as optional. Fully qualified because
    // this namespace is nested under NetworkSimulator, where a bare "Application" resolves to the
    // NetSim.Application *namespace* instead - see docs/CONTEXT.md, section 6.
    private static StreamGeometry? ResolveIcon(string resourceKey) =>
        global::Avalonia.Application.Current?.TryFindResource(resourceKey, out var resource) == true
            ? resource as StreamGeometry
            : null;
}
