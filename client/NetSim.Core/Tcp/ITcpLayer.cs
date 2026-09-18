using System.Diagnostics.CodeAnalysis;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;

namespace NetSim.Core.Tcp;

/// <summary>
/// The TCP layer's protocol-mechanics service surface: builds segments, bridges them to and from
/// IPv4 (<see cref="ProtocolNumber.Tcp"/> encapsulation/decapsulation), runs received segments
/// through <see cref="TcpProcessor"/>, and gates an inbound segment (structural validity, checksum,
/// destination ownership) before handing it to the injected <see cref="ITcpConnectionManager"/> for
/// the actual state-machine decision. The TCP counterpart to <see cref="Icmp.IIcmpLayer"/> - it does
/// not itself track connections; see <see cref="ITcpConnectionManager"/> for that.
/// </summary>
public interface ITcpLayer
{
    /// <summary>Builds a <see cref="TcpSegment"/> (real checksum for the given addresses) and raises <see cref="SegmentCreated"/>. A thin, event-raising wrapper over <see cref="TcpSegment.Create"/> for callers outside <see cref="ITcpConnectionManager"/>.</summary>
    TcpSegment CreateSegment(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort,
        uint sequenceNumber, uint acknowledgmentNumber, TcpFlags flags,
        ushort windowSize = TcpSegment.DefaultWindowSize, IPacketPayload? payload = null);

    /// <summary>Wraps <paramref name="segment"/> in an <see cref="IPv4Packet"/> with <see cref="ProtocolNumber.Tcp"/> and raises <see cref="SegmentEncapsulated"/>.</summary>
    IPv4Packet Encapsulate(TcpSegment segment, IPv4Address source, IPv4Address destination, byte timeToLive = IPv4Packet.DefaultTimeToLive);

    /// <summary>Extracts the <see cref="TcpSegment"/> from an IPv4 packet whose protocol is TCP. False otherwise.</summary>
    bool TryDecapsulate(IPv4Packet packet, [NotNullWhen(true)] out TcpSegment? segment);

    /// <summary>Runs <paramref name="packet"/> through <see cref="TcpProcessor"/>, raising <see cref="PacketProcessed"/> / <see cref="PacketDropped"/>.</summary>
    PacketProcessingResult Process(Packet packet);

    /// <summary>
    /// Processes a TCP segment carried by <paramref name="ipPacket"/> that arrived on
    /// <paramref name="receivingInterface"/>: validates structure and checksum, confirms the
    /// interface owns the IPv4 destination (never forwarded - no routing), then delegates the
    /// state-machine decision to the injected <see cref="ITcpConnectionManager"/>.
    /// </summary>
    TcpProcessingReport HandleIncoming(NetworkInterface receivingInterface, IPv4Packet ipPacket);

    event EventHandler<TcpEventArgs>? SegmentCreated;

    event EventHandler<TcpEventArgs>? SegmentEncapsulated;

    event EventHandler<TcpEventArgs>? PacketProcessed;

    /// <summary>Raised when a received segment is dropped (not TCP, structurally invalid, a checksum mismatch, or an unowned destination).</summary>
    event EventHandler<TcpEventArgs>? PacketDropped;
}
