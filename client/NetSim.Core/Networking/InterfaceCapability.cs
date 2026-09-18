namespace NetSim.Core.Networking;

/// <summary>
/// The media families a <see cref="NetworkInterface"/> can physically participate in. Modelled as
/// flags because a single interface can legitimately support more than one (e.g. a Gigabit port
/// that accepts either copper or an SFP fibre module). Two interfaces are cable-compatible when
/// their capability sets overlap - see <see cref="ConnectionCompatibility"/>. This is deliberately
/// a physical-media concept only; switching/routing/VLAN behaviour is layered on in later phases.
/// </summary>
[Flags]
public enum InterfaceCapability
{
    None = 0,
    Ethernet = 1 << 0,
    Serial = 1 << 1,
    Console = 1 << 2,
    Fiber = 1 << 3,
    Wireless = 1 << 4,
}
