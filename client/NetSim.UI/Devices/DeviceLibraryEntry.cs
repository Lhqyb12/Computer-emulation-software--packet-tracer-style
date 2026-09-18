using Avalonia.Media;
using NetSim.Core.Devices;

namespace NetSim.UI.Devices;

/// <summary>
/// One Device Library card's worth of display data - everything the view needs, and nothing the
/// domain doesn't have: <see cref="DeviceType"/>/<see cref="DisplayName"/> come straight from
/// <c>DeviceTypeRegistry</c> (Core), while <see cref="Category"/>/<see cref="Description"/>/
/// <see cref="Icon"/> are UI-only presentation concerns that live here instead of on the domain
/// type - see docs/architecture/device-model.md, "Visual identity".
/// </summary>
public sealed record DeviceLibraryEntry(
    DeviceType DeviceType,
    string DisplayName,
    DeviceCategory Category,
    string Description,
    StreamGeometry? Icon);
