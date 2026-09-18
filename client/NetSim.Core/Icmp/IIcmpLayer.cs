using System.Diagnostics.CodeAnalysis;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Icmp;

/// <summary>
/// The ICMP engine's service surface: it builds Echo Request / Echo Reply (and, as a foundation,
/// Destination Unreachable / Time Exceeded) messages, bridges them to and from the IPv4 layer
/// (<see cref="ProtocolNumber.Icmp"/> encapsulation / decapsulation), runs received messages
/// through the <see cref="IcmpProcessor"/>, decides whether an Echo Request is answered (the
/// receiving interface must own the IPv4 destination - never forwarded, no routing), and raises
/// ICMP events for later phases (visualization, timeline, monitoring, diagnostics, AI).
///
/// It is the ICMP counterpart to <see cref="Arp.IArpLayer"/>: like ARP, it does <em>not</em> move
/// packets across cables (that stays the Ethernet layer's single physical hop, reached through
/// <see cref="IIPv4Layer.Encapsulate"/> + <see cref="Ethernet.IEthernetTransmissionService.Transmit"/>)
/// and it makes no routing decision. Unlike ARP, ICMP rides <em>inside</em> IPv4 rather than
/// directly inside Ethernet - it never gets its own EtherType.
/// </summary>
public interface IIcmpLayer
{
    /// <summary>Builds an Echo Request. Raises <see cref="EchoRequestCreated"/>.</summary>
    IcmpMessage CreateEchoRequest(ushort identifier, ushort sequenceNumber, RawPayload? data = null);

    /// <summary>
    /// Builds the Echo Reply that answers <paramref name="request"/> (same identifier, sequence
    /// number and data). Raises <see cref="EchoReplyCreated"/>. Throws
    /// <see cref="Common.Exceptions.DomainException"/> if <paramref name="request"/> is not an Echo
    /// Request.
    /// </summary>
    IcmpMessage CreateEchoReply(IcmpMessage request);

    /// <summary>
    /// Builds a Time Exceeded message about <paramref name="originalDatagram"/> - the Phase 21
    /// foundation for a future Traceroute / forwarding phase. Nothing in this phase calls this from
    /// the receive path (no forwarding engine decrements TTL to zero yet).
    /// </summary>
    IcmpMessage CreateTimeExceeded(IcmpOriginalDatagramInfo originalDatagram, IcmpCode? code = null);

    /// <summary>
    /// Builds a Destination Unreachable message about <paramref name="originalDatagram"/> with the
    /// given reason - the Phase 21 foundation for a future routing / transport phase.
    /// </summary>
    IcmpMessage CreateDestinationUnreachable(IcmpOriginalDatagramInfo originalDatagram, IcmpCode code);

    /// <summary>
    /// Wraps <paramref name="message"/> in an <see cref="IPv4Packet"/> with
    /// <see cref="ProtocolNumber.Icmp"/> and raises <see cref="EchoRequestEncapsulated"/> /
    /// <see cref="EchoReplyEncapsulated"/> as appropriate. The packet is then encapsulated in an
    /// Ethernet frame by <see cref="IIPv4Layer.Encapsulate"/> exactly like any other IPv4 packet.
    /// </summary>
    IPv4Packet Encapsulate(IcmpMessage message, IPv4Address source, IPv4Address destination, byte timeToLive = IPv4Packet.DefaultTimeToLive);

    /// <summary>Extracts the <see cref="IcmpMessage"/> from an IPv4 packet whose protocol is ICMP. False for any other protocol.</summary>
    bool TryDecapsulate(IPv4Packet packet, [NotNullWhen(true)] out IcmpMessage? message);

    /// <summary>
    /// Runs <paramref name="packet"/> through the ICMP processor and raises
    /// <see cref="PacketProcessed"/> or <see cref="PacketDropped"/> according to the result. Does
    /// not itself change the packet's engine state - pass the same processor to
    /// <see cref="IPacketEngine.Process"/> for that.
    /// </summary>
    PacketProcessingResult Process(Packet packet);

    /// <summary>
    /// Processes an ICMP message carried by <paramref name="ipPacket"/> that arrived on
    /// <paramref name="receivingInterface"/>: validates it (structure and checksum), and for an Echo
    /// Request whose IPv4 destination the interface owns, builds the Echo Reply and its IPv4 packet
    /// (source/destination swapped). Never answers a request for an address the interface does not
    /// own - never forwarded, no routing. Returns a descriptive <see cref="IcmpProcessingReport"/>.
    /// Raises the matching received / dropped events.
    /// </summary>
    IcmpProcessingReport HandleIncoming(NetworkInterface receivingInterface, IPv4Packet ipPacket);

    /// <summary>Raised after an Echo Request is built.</summary>
    event EventHandler<IcmpEventArgs>? EchoRequestCreated;

    /// <summary>Raised after an Echo Request is wrapped in its IPv4 packet.</summary>
    event EventHandler<IcmpEventArgs>? EchoRequestEncapsulated;

    /// <summary>Raised after an Echo Reply is built.</summary>
    event EventHandler<IcmpEventArgs>? EchoReplyCreated;

    /// <summary>Raised after an Echo Reply is wrapped in its IPv4 packet.</summary>
    event EventHandler<IcmpEventArgs>? EchoReplyEncapsulated;

    /// <summary>Raised when a valid Echo Request is received (before the ownership decision).</summary>
    event EventHandler<IcmpEventArgs>? EchoRequestReceived;

    /// <summary>Raised when a valid Echo Reply is received.</summary>
    event EventHandler<IcmpEventArgs>? EchoReplyReceived;

    /// <summary>Raised when a valid Destination Unreachable or Time Exceeded message is received.</summary>
    event EventHandler<IcmpEventArgs>? ErrorMessageReceived;

    /// <summary>Raised after <see cref="Process"/> accepts a packet at the ICMP layer.</summary>
    event EventHandler<IcmpEventArgs>? PacketProcessed;

    /// <summary>Raised when a received message is dropped (not ICMP, structurally invalid, or a checksum mismatch).</summary>
    event EventHandler<IcmpEventArgs>? PacketDropped;
}
