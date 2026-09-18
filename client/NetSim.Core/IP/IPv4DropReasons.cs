using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// The IPv4 layer's catalogue of structured drop reasons - plain <see cref="PacketDropReason"/>
/// values (the Phase 16 type) with an <c>IPV4_</c> code prefix, exactly the "each protocol layer
/// contributes its own catalogue" extension point the packet engine anticipated. Network-layer
/// causes only: no routing / ARP / ICMP reasons belong here.
/// </summary>
public static class IPv4DropReasons
{
    /// <summary>The packet does not carry an IPv4 packet (nor an Ethernet frame with EtherType IPv4 wrapping one).</summary>
    public static PacketDropReason NotIPv4 { get; } = new("IPV4_NOT_IPV4", "The packet does not carry an IPv4 packet.");

    /// <summary>The IPv4 packet failed structural validation (bad length, out-of-range field, broken payload chain).</summary>
    public static PacketDropReason InvalidPacket { get; } = new("IPV4_INVALID_PACKET", "The IPv4 packet failed structural validation.");

    /// <summary>The IPv4 packet's TTL has reached zero - it can no longer live on the network.</summary>
    public static PacketDropReason TimeToLiveExpired { get; } = new("IPV4_TTL_EXPIRED", "The IPv4 packet's TTL has reached zero.");

    /// <summary>The IPv4 source address is not a valid host address.</summary>
    public static PacketDropReason InvalidSourceAddress { get; } = new("IPV4_INVALID_SRC", "The IPv4 source address is not a valid host address.");

    /// <summary>The IPv4 destination address is not set.</summary>
    public static PacketDropReason InvalidDestinationAddress { get; } = new("IPV4_INVALID_DST", "The IPv4 destination address is not set.");
}
