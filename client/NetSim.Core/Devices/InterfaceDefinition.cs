using NetSim.Core.Networking;

namespace NetSim.Core.Devices;

/// <summary>
/// A declarative description of one interface a device type should start life with: its stable
/// <see cref="Name"/> and its <see cref="InterfaceType"/>. Speed and capabilities are derived from
/// the type (see <see cref="InterfaceTypeInfo"/>), so a device blueprint stays a plain list of
/// name/type pairs. Turned into a live <see cref="NetworkInterface"/> by
/// <see cref="NetworkDevice.AddInterface"/>.
/// </summary>
public sealed record InterfaceDefinition(string Name, InterfaceType InterfaceType);
