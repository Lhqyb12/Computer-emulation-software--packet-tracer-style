using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// The IPv6 layer's catalogue of structured drop reasons - plain <see cref="PacketDropReason"/>
/// values (the Phase 16 type) with an <c>IPV6_</c> code prefix, exactly the "each protocol layer
/// contributes its own catalogue" extension point the packet engine anticipated. Network-layer
/// causes only: no routing / Neighbor Discovery / ICMPv6 reasons belong here.
/// </summary>
public static class IPv6DropReasons
{
    /// <summary>The packet does not carry an IPv6 packet (nor an Ethernet frame with EtherType IPv6 wrapping one).</summary>
    public static PacketDropReason NotIPv6 { get; } = new("IPV6_NOT_IPV6", "The packet does not carry an IPv6 packet.");

    /// <summary>The IPv6 packet failed structural validation (bad field, inconsistent length, broken payload chain).</summary>
    public static PacketDropReason InvalidPacket { get; } = new("IPV6_INVALID_PACKET", "The IPv6 packet failed structural validation.");

    /// <summary>The IPv6 packet's Hop Limit has reached zero - it can no longer live on the network.</summary>
    public static PacketDropReason HopLimitExpired { get; } = new("IPV6_HOP_LIMIT_EXPIRED", "The IPv6 packet's Hop Limit has reached zero.");

    /// <summary>The IPv6 source address is not a valid packet source (e.g. a multicast address).</summary>
    public static PacketDropReason InvalidSourceAddress { get; } = new("IPV6_INVALID_SRC", "The IPv6 source address is not a valid packet source.");

    /// <summary>The IPv6 destination address is not set.</summary>
    public static PacketDropReason InvalidDestinationAddress { get; } = new("IPV6_INVALID_DST", "The IPv6 destination address is not set.");
}
