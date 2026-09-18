using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// The IPv4 layer's service surface: it builds IPv4 packets, bridges them to and from the Ethernet
/// layer (<see cref="EtherType.IPv4"/> encapsulation / decapsulation), runs them through the
/// <see cref="IPv4Processor"/>, and raises IPv4 packet events for later phases (visualization,
/// monitoring, diagnostics). It is the IPv4 counterpart to
/// <see cref="Ethernet.IEthernetTransmissionService"/> - it does <em>not</em> move packets across
/// cables (that stays the Ethernet layer's single physical hop) and it makes no routing decision.
/// </summary>
public interface IIPv4Layer
{
    /// <summary>Builds an <see cref="IPv4Packet"/> (see <see cref="IPv4Packet.Create"/>) and raises <see cref="PacketCreated"/>.</summary>
    IPv4Packet CreatePacket(
        IPv4Address source,
        IPv4Address destination,
        IPacketPayload? payload = null,
        ProtocolNumber protocol = default,
        byte timeToLive = IPv4Packet.DefaultTimeToLive,
        ushort identification = 0,
        IPv4Flags flags = IPv4Flags.None,
        int fragmentOffset = 0);

    /// <summary>
    /// Wraps <paramref name="packet"/> in an <see cref="EthernetFrame"/> with
    /// <see cref="EtherType.IPv4"/> and raises <see cref="PacketEncapsulated"/>. The frame is then
    /// transmitted by the Ethernet layer exactly as any other frame.
    /// </summary>
    EthernetFrame Encapsulate(IPv4Packet packet, MacAddress sourceMac, MacAddress destinationMac);

    /// <summary>
    /// Extracts the <see cref="IPv4Packet"/> from an Ethernet frame whose EtherType is IPv4.
    /// Returns false (and a null <paramref name="packet"/>) for any other frame.
    /// </summary>
    bool TryDecapsulate(EthernetFrame frame, [NotNullWhen(true)] out IPv4Packet? packet);

    /// <summary>
    /// Runs <paramref name="packet"/> through the IPv4 processor and raises
    /// <see cref="PacketProcessed"/> or <see cref="PacketDropped"/> according to the result. Does
    /// not itself change the packet's engine state - pass the same processor to
    /// <see cref="IPacketEngine.Process"/> for that.
    /// </summary>
    PacketProcessingResult Process(Packet packet);

    /// <summary>Raised after a packet is built through <see cref="CreatePacket"/>.</summary>
    event EventHandler<IPv4PacketEventArgs>? PacketCreated;

    /// <summary>Raised after a packet is wrapped in an Ethernet frame through <see cref="Encapsulate"/>.</summary>
    event EventHandler<IPv4PacketEventArgs>? PacketEncapsulated;

    /// <summary>Raised after <see cref="Process"/> accepts a packet at the IPv4 layer.</summary>
    event EventHandler<IPv4PacketEventArgs>? PacketProcessed;

    /// <summary>Raised after <see cref="Process"/> rejects a packet, carrying the <see cref="IPv4PacketEventArgs.DropReason"/>.</summary>
    event EventHandler<IPv4PacketEventArgs>? PacketDropped;
}
