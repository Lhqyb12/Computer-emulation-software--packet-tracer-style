using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;

namespace NetSim.Core.Udp;

/// <summary>
/// The UDP layer's service surface: builds datagrams, bridges them to and from IPv4
/// (<see cref="ProtocolNumber.Udp"/> encapsulation/decapsulation), runs received datagrams through
/// <see cref="UdpProcessor"/>, decides delivery via the injected <see cref="IUdpDeliveryManager"/>,
/// and raises UDP events. The UDP counterpart to <see cref="Icmp.IIcmpLayer"/> - connectionless, so
/// there is no handshake, no sequencing, and no retransmission (brief section 8).
/// </summary>
public interface IUdpLayer
{
    /// <summary>Builds a <see cref="UdpDatagram"/> (real checksum for the given addresses) and raises <see cref="DatagramCreated"/>.</summary>
    UdpDatagram CreateDatagram(IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort, IPacketPayload? payload = null);

    /// <summary>Wraps <paramref name="datagram"/> in an <see cref="IPv4Packet"/> with <see cref="ProtocolNumber.Udp"/> and raises <see cref="DatagramEncapsulated"/>.</summary>
    IPv4Packet Encapsulate(UdpDatagram datagram, IPv4Address source, IPv4Address destination, byte timeToLive = IPv4Packet.DefaultTimeToLive);

    /// <summary>Extracts the <see cref="UdpDatagram"/> from an IPv4 packet whose protocol is UDP. False otherwise.</summary>
    bool TryDecapsulate(IPv4Packet packet, [NotNullWhen(true)] out UdpDatagram? datagram);

    /// <summary>Runs <paramref name="packet"/> through <see cref="UdpProcessor"/>, raising <see cref="PacketProcessed"/> / <see cref="PacketDropped"/>.</summary>
    PacketProcessingResult Process(Packet packet);

    /// <summary>
    /// Processes a UDP datagram carried by <paramref name="ipPacket"/> that arrived on
    /// <paramref name="receivingInterface"/>: validates structure and checksum, confirms the
    /// interface owns the IPv4 destination (never forwarded - no routing), then attempts delivery
    /// through <see cref="IUdpDeliveryManager"/>. Raises the matching received/delivered/port-unavailable/dropped events.
    /// </summary>
    UdpProcessingReport HandleIncoming(NetworkInterface receivingInterface, IPv4Packet ipPacket);

    event EventHandler<UdpEventArgs>? DatagramCreated;

    event EventHandler<UdpEventArgs>? DatagramEncapsulated;

    /// <summary>Raised when a valid datagram is received, before the delivery decision.</summary>
    event EventHandler<UdpEventArgs>? DatagramReceived;

    /// <summary>Raised after a datagram is handed to a bound endpoint.</summary>
    event EventHandler<UdpEventArgs>? DatagramDelivered;

    /// <summary>Raised when a structurally valid, checksum-correct datagram has no bound destination endpoint.</summary>
    event EventHandler<UdpEventArgs>? PortUnavailable;

    event EventHandler<UdpEventArgs>? PacketProcessed;

    /// <summary>Raised when a received datagram is dropped (not UDP, structurally invalid, or a checksum mismatch).</summary>
    event EventHandler<UdpEventArgs>? PacketDropped;
}
