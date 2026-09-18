namespace NetSim.UI.Devices;

/// <summary>
/// A Device Library grouping used only for browsing/filtering - a UI-only concept, deliberately
/// not added to <c>NetSim.Core.Devices.DeviceType</c>/<c>DeviceTypeRegistry</c> (see
/// docs/architecture/device-model.md, "What was deliberately not added": Core has no concept of
/// device categories today, and nothing in the domain needs one). Every value here exists so the
/// Device Library can show a fixed set of category tabs even for a category with zero supported
/// devices yet (Wireless/Security/Other) - see <see cref="DeviceCatalog"/>.
/// </summary>
public enum DeviceCategory
{
    EndDevices,
    NetworkDevices,
    Wireless,
    Security,
    Other,
}
