using NetSim.Core.Packets;

namespace NetSim.Core.Icmp;

/// <summary>
/// The ICMP engine's catalogue of structured drop reasons - plain <see cref="PacketDropReason"/>
/// values (the Phase 16 type) with an <c>ICMP_</c> code prefix, exactly the "each protocol layer
/// contributes its own catalogue" extension point the packet engine anticipated. An Echo Request
/// addressed to an IPv4 the receiving interface does not own is deliberately NOT a drop reason here
/// - like ARP's "target not owned", it is a normal, successful processing outcome that simply
/// produces no reply (see <see cref="IcmpProcessingReport"/>).
/// </summary>
public static class IcmpDropReasons
{
    /// <summary>The packet does not carry an ICMP message (nor an IPv4 packet with Protocol ICMP wrapping one).</summary>
    public static PacketDropReason NotIcmp { get; } = new("ICMP_NOT_ICMP", "The packet does not carry an ICMP message.");

    /// <summary>The ICMP message failed structural validation (unknown type, non-zero code on an echo message, missing original-datagram info on an error message).</summary>
    public static PacketDropReason InvalidPacket { get; } = new("ICMP_INVALID_PACKET", "The ICMP message failed structural validation.");

    /// <summary>The ICMP checksum does not match the message's contents - it was corrupted in transit.</summary>
    public static PacketDropReason InvalidChecksum { get; } = new("ICMP_INVALID_CHECKSUM", "The ICMP checksum does not match the message contents.");
}
