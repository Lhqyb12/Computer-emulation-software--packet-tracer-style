using NetSim.Core.Packets;

namespace NetSim.Core.Arp;

/// <summary>
/// The ARP engine's catalogue of structured drop reasons - plain <see cref="PacketDropReason"/>
/// values (the Phase 16 type) with an <c>ARP_</c> code prefix, exactly the "each protocol layer
/// contributes its own catalogue" extension point the packet engine anticipated. Address-resolution
/// causes only: no routing / ICMP / IPv6 reasons belong here.
/// </summary>
public static class ArpDropReasons
{
    /// <summary>The packet does not carry an ARP message (nor an Ethernet frame with EtherType ARP wrapping one).</summary>
    public static PacketDropReason NotArp { get; } = new("ARP_NOT_ARP", "The packet does not carry an ARP message.");

    /// <summary>The ARP message failed structural validation (bad hardware/protocol type, wrong address length, bad operation, invalid address).</summary>
    public static PacketDropReason InvalidPacket { get; } = new("ARP_INVALID_PACKET", "The ARP message failed structural validation.");

    /// <summary>The ARP operation is neither Request nor Reply.</summary>
    public static PacketDropReason UnsupportedOperation { get; } = new("ARP_UNSUPPORTED_OPERATION", "The ARP operation is neither Request nor Reply.");

    /// <summary>The interface asked to resolve an address has no MAC and/or no IPv4 address to build an ARP request from.</summary>
    public static PacketDropReason InvalidSenderInterface { get; } = new("ARP_INVALID_SENDER_IF", "The sending interface has no MAC and/or IPv4 address for ARP.");
}
