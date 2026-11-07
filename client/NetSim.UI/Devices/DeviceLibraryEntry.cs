using Avalonia.Media;        // StreamGeometry - the shape of an icon
using NetSim.Core.Devices;   // DeviceType

namespace NetSim.UI.Devices;   // the namespace = the "address" of the classes in this file; other files reach them with "using"

// Everything one card in the Device Library shows.
// DeviceType and DisplayName come from Core (DeviceTypeRegistry); Category, Description and Icon
// are only for the screen, so they are added here.
// "StreamGeometry?" = the icon shape, or null if it was not found
public sealed record DeviceLibraryEntry(
    DeviceType DeviceType,     // which kind of device
    string DisplayName,        // the name on the card, e.g. "PC"
    DeviceCategory Category,   // its group, for the filter buttons
    string Description,        // one short sentence under the name
    StreamGeometry? Icon);     // the drawing on the card
