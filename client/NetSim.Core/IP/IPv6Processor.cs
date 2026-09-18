using NetSim.Core.Ethernet;
using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// An <see cref="IPacketProcessor"/> that handles a <see cref="Packet"/> at the IPv6 layer
/// <em>only</em>. It accepts either a packet whose payload is an <see cref="IPv6Packet"/>
/// directly, or one carrying an <see cref="EthernetFrame"/> with <see cref="EtherType.IPv6"/> (the
/// Ethernet -&gt; IPv6 decapsulation path). It confirms the IPv6 packet is structurally valid and
/// that its Hop Limit has not expired, then reports that the packet would be delivered up the stack.
///
/// It performs no routing, no forwarding, no Neighbor Discovery, no ICMPv6 and no TCP/UDP handling
/// - those are later phases. Like <see cref="IPv4Processor"/> it only <em>decides</em> the outcome;
/// the <see cref="IPacketEngine"/> applies the state change and raises events.
/// </summary>
public sealed class IPv6Processor : IPacketProcessor
{
    public PacketProcessingResult Process(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (!TryExtract(packet.Payload, out var ipPacket, out var extractionError))
        {
            return PacketProcessingResult.Invalid(extractionError!);
        }

        var validation = ipPacket!.Validate();
        if (!validation.IsValid)
        {
            return PacketProcessingResult.Dropped(
                IPv6DropReasons.InvalidPacket,
                string.Join("; ", validation.Errors));
        }

        if (ipPacket.IsHopLimitExhausted)
        {
            return PacketProcessingResult.Dropped(
                IPv6DropReasons.HopLimitExpired,
                $"Hop Limit 0 for {ipPacket}");
        }

        return PacketProcessingResult.Delivered(Describe(ipPacket));
    }

    /// <summary>
    /// Pulls the <see cref="IPv6Packet"/> out of a packet payload: it is the payload itself, or it
    /// is the payload of an Ethernet frame whose EtherType is IPv6. Returns false with an
    /// explanatory <paramref name="error"/> otherwise (including a frame that claims EtherType IPv6
    /// but carries something else).
    /// </summary>
    internal static bool TryExtract(IPacketPayload? payload, out IPv6Packet? ipPacket, out string? error)
    {
        switch (payload)
        {
            case IPv6Packet direct:
                ipPacket = direct;
                error = null;
                return true;

            case EthernetFrame frame when frame.EtherType == EtherType.IPv6 && frame.Payload is IPv6Packet fromFrame:
                ipPacket = fromFrame;
                error = null;
                return true;

            case EthernetFrame frame when frame.EtherType == EtherType.IPv6:
                ipPacket = null;
                error = "Ethernet frame declares EtherType IPv6 but its payload is not an IPv6 packet.";
                return false;

            case EthernetFrame frame:
                ipPacket = null;
                error = $"Ethernet frame does not carry IPv6 (EtherType {frame.EtherType.Name}).";
                return false;

            default:
                ipPacket = null;
                error = "Packet does not carry an IPv6 packet.";
                return false;
        }
    }

    private static string Describe(IPv6Packet packet) =>
        $"IPv6 {packet.SourceAddress} -> {packet.DestinationAddress}, next header {packet.NextHeader.Name}, hop limit {packet.HopLimit}";
}
