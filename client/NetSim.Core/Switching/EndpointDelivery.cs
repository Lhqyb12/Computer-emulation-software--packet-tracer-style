using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Switching;

/// <summary>
/// One frame that reached a non-switch interface at the edge of a Layer 2 segment - the point an
/// upper layer (ARP / IPv4 / ICMP / UDP / DHCP orchestration) picks the frame up. Carries the
/// receiving <see cref="DestinationInterface"/>, the delivered <see cref="Frame"/> and the tracked
/// <see cref="Packet"/> the Ethernet layer created for this final hop, so every forwarding path
/// stays independently traceable.
/// </summary>
public sealed record EndpointDelivery(
    NetworkInterface DestinationInterface,
    EthernetFrame Frame,
    Packet Packet);
