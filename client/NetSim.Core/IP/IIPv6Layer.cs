using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// The IPv6 layer's service surface: it builds IPv6 packets, bridges them to and from the Ethernet
/// layer (<see cref="EtherType.IPv6"/> encapsulation / decapsulation), runs them through the
/// <see cref="IPv6Processor"/>, and raises IPv6 packet events for later phases (visualization,
/// monitoring, diagnostics). It is the IPv6 counterpart to <see cref="IIPv4Layer"/> - it does
/// <em>not</em> move packets across cables (that stays the Ethernet layer's single physical hop)
/// and it makes no routing decision.
/// </summary>
public interface IIPv6Layer
{
    /// <summary>Builds an <see cref="IPv6Packet"/> (see <see cref="IPv6Packet.Create"/>) and raises <see cref="PacketCreated"/>.</summary>
    IPv6Packet CreatePacket(
        IPv6Address source,
        IPv6Address destination,
        IPacketPayload? payload = null,
        NextHeader nextHeader = default,
        byte hopLimit = IPv6Packet.DefaultHopLimit,
        byte trafficClass = 0,
        int flowLabel = 0);

    /// <summary>
    /// Wraps <paramref name="packet"/> in an <see cref="EthernetFrame"/> with
    /// <see cref="EtherType.IPv6"/> and raises <see cref="PacketEncapsulated"/>. The frame is then
    /// transmitted by the Ethernet layer exactly as any other frame.
    /// </summary>
    EthernetFrame Encapsulate(IPv6Packet packet, MacAddress sourceMac, MacAddress destinationMac);

    /// <summary>
    /// Extracts the <see cref="IPv6Packet"/> from an Ethernet frame whose EtherType is IPv6.
    /// Returns false (and a null <paramref name="packet"/>) for any other frame.
    /// </summary>
    bool TryDecapsulate(EthernetFrame frame, [NotNullWhen(true)] out IPv6Packet? packet);

    /// <summary>
    /// Runs <paramref name="packet"/> through the IPv6 processor and raises
    /// <see cref="PacketProcessed"/> or <see cref="PacketDropped"/> according to the result. Does
    /// not itself change the packet's engine state - pass the same processor to
    /// <see cref="IPacketEngine.Process"/> for that.
    /// </summary>
    PacketProcessingResult Process(Packet packet);

    /// <summary>Raised after a packet is built through <see cref="CreatePacket"/>.</summary>
    event EventHandler<IPv6PacketEventArgs>? PacketCreated;

    /// <summary>Raised after a packet is wrapped in an Ethernet frame through <see cref="Encapsulate"/>.</summary>
    event EventHandler<IPv6PacketEventArgs>? PacketEncapsulated;

    /// <summary>Raised after <see cref="Process"/> accepts a packet at the IPv6 layer.</summary>
    event EventHandler<IPv6PacketEventArgs>? PacketProcessed;

    /// <summary>Raised after <see cref="Process"/> rejects a packet, carrying the <see cref="IPv6PacketEventArgs.DropReason"/>.</summary>
    event EventHandler<IPv6PacketEventArgs>? PacketDropped;
}
