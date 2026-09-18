using NetSim.Core.Packets;

namespace NetSim.Core.Udp;

/// <summary>
/// The UDP engine's catalogue of structured drop reasons - the same "each protocol layer
/// contributes its own catalogue" pattern as <see cref="Icmp.IcmpDropReasons"/> / <see cref="Arp.ArpDropReasons"/>.
/// </summary>
public static class UdpDropReasons
{
    /// <summary>The packet does not carry a UDP datagram wrapped in an IPv4 packet.</summary>
    public static PacketDropReason NotUdp { get; } = new("UDP_NOT_UDP", "The packet does not carry a UDP datagram.");

    /// <summary>The UDP datagram (or its payload) failed structural validation.</summary>
    public static PacketDropReason InvalidPacket { get; } = new("UDP_INVALID_PACKET", "The UDP datagram failed structural validation.");

    /// <summary>The UDP checksum does not match the datagram's contents/addresses - it was corrupted in transit.</summary>
    public static PacketDropReason InvalidChecksum { get; } = new("UDP_INVALID_CHECKSUM", "The UDP checksum does not match the datagram contents.");

    /// <summary>
    /// No endpoint is bound to the datagram's destination port on the receiving device. A real stack
    /// would answer with an ICMP Destination Unreachable (Port Unreachable) message; no ICMP
    /// forwarding hook exists yet, so this is surfaced only as a structured event/report for now -
    /// see <see cref="IUdpLayer.PortUnavailable"/>.
    /// </summary>
    public static PacketDropReason PortUnavailable { get; } = new("UDP_PORT_UNAVAILABLE", "No endpoint is bound to the destination port.");
}
