namespace NetSim.Core.Devices;

/// <summary>
/// Metadata about a supported <see cref="Devices.DeviceType"/> that a UI can use to list it
/// (e.g. the future Device Library) without hard-coding display strings or depending on the
/// concrete device class. Deliberately does not carry an icon/visual resource - that mapping
/// belongs to the UI layer (see docs/architecture/device-model.md, "Visual identity").
/// </summary>
public sealed record DeviceTypeDescriptor(DeviceType DeviceType, string DisplayName);
