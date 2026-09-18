using NetSim.Core.Ethernet;
using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// An <see cref="IPacketProcessor"/> that handles a <see cref="Packet"/> at the IPv4 layer
/// <em>only</em>. It accepts either a packet whose payload is an <see cref="IPv4Packet"/>
/// directly, or one carrying an <see cref="EthernetFrame"/> with
/// <see cref="EtherType.IPv4"/> (the Ethernet -&gt; IPv4 decapsulation path). It confirms the
/// IPv4 packet is structurally valid and that its TTL has not expired, then reports that the
/// packet would be delivered up the stack.
///
/// It performs no routing, no forwarding, no ARP, no ICMP and no TCP/UDP handling - those are
/// later phases. Like <see cref="EthernetProcessor"/> it only <em>decides</em> the outcome; the
/// <see cref="IPacketEngine"/> applies the state change and raises events.
/// </summary>
public sealed class IPv4Processor : IPacketProcessor
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
                IPv4DropReasons.InvalidPacket,
                string.Join("; ", validation.Errors));
        }

        if (ipPacket.IsTimeToLiveExhausted)
        {
            return PacketProcessingResult.Dropped(
                IPv4DropReasons.TimeToLiveExpired,
                $"TTL 0 for {ipPacket}");
        }

        return PacketProcessingResult.Delivered(Describe(ipPacket));
    }

    /// <summary>
    /// Pulls the <see cref="IPv4Packet"/> out of a packet payload: it is the payload itself, or it
    /// is the payload of an Ethernet frame whose EtherType is IPv4. Returns false with an
    /// explanatory <paramref name="error"/> otherwise (including a frame that claims EtherType IPv4
    /// but carries something else).
    /// </summary>
    internal static bool TryExtract(IPacketPayload? payload, out IPv4Packet? ipPacket, out string? error)
    {
        switch (payload)
        {
            case IPv4Packet direct:
                ipPacket = direct;
                error = null;
                return true;

            case EthernetFrame frame when frame.EtherType == EtherType.IPv4 && frame.Payload is IPv4Packet fromFrame:
                ipPacket = fromFrame;
                error = null;
                return true;

            case EthernetFrame frame when frame.EtherType == EtherType.IPv4:
                ipPacket = null;
                error = "Ethernet frame declares EtherType IPv4 but its payload is not an IPv4 packet.";
                return false;

            case EthernetFrame frame:
                ipPacket = null;
                error = $"Ethernet frame does not carry IPv4 (EtherType {frame.EtherType.Name}).";
                return false;

            default:
                ipPacket = null;
                error = "Packet does not carry an IPv4 packet.";
                return false;
        }
    }

    private static string Describe(IPv4Packet packet) =>
        $"IPv4 {packet.SourceAddress} -> {packet.DestinationAddress}, protocol {packet.Protocol.Name}, ttl {packet.TimeToLive}";
}
