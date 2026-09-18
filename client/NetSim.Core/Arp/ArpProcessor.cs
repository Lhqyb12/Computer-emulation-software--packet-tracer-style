using NetSim.Core.Ethernet;
using NetSim.Core.Packets;

namespace NetSim.Core.Arp;

/// <summary>
/// An <see cref="IPacketProcessor"/> that handles a <see cref="Packet"/> at the ARP layer
/// <em>only</em>. It accepts either a packet whose payload is an <see cref="ArpPacket"/> directly,
/// or one carrying an <see cref="EthernetFrame"/> with <see cref="EtherType.Arp"/> (the
/// Ethernet -&gt; ARP decapsulation path). It confirms the ARP message is structurally valid, then
/// reports that it would be delivered up the stack.
///
/// It performs no cache updates, no reply generation and no ownership decision - that stateful,
/// interface-scoped work is <see cref="ArpLayer"/>'s. Like <see cref="EthernetProcessor"/> /
/// <see cref="IP.IPv4Processor"/> it only <em>decides</em> the outcome; the
/// <see cref="IPacketEngine"/> applies the state change and raises events.
/// </summary>
public sealed class ArpProcessor : IPacketProcessor
{
    public PacketProcessingResult Process(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (!TryExtract(packet.Payload, out var arp, out var extractionError))
        {
            return PacketProcessingResult.Invalid(extractionError!);
        }

        var validation = arp!.Validate();
        if (!validation.IsValid)
        {
            return PacketProcessingResult.Dropped(
                ArpDropReasons.InvalidPacket,
                string.Join("; ", validation.Errors));
        }

        return PacketProcessingResult.Delivered(Describe(arp));
    }

    /// <summary>
    /// Pulls the <see cref="ArpPacket"/> out of a packet payload: it is the payload itself, or it is
    /// the payload of an Ethernet frame whose EtherType is ARP. Returns false with an explanatory
    /// <paramref name="error"/> otherwise (including a frame that claims EtherType ARP but carries
    /// something else). Mirrors <see cref="IP.IPv4Processor.TryExtract"/>.
    /// </summary>
    internal static bool TryExtract(IPacketPayload? payload, out ArpPacket? arp, out string? error)
    {
        switch (payload)
        {
            case ArpPacket direct:
                arp = direct;
                error = null;
                return true;

            case EthernetFrame frame when frame.EtherType == EtherType.Arp && frame.Payload is ArpPacket fromFrame:
                arp = fromFrame;
                error = null;
                return true;

            case EthernetFrame frame when frame.EtherType == EtherType.Arp:
                arp = null;
                error = "Ethernet frame declares EtherType ARP but its payload is not an ARP message.";
                return false;

            case EthernetFrame frame:
                arp = null;
                error = $"Ethernet frame does not carry ARP (EtherType {frame.EtherType.Name}).";
                return false;

            default:
                arp = null;
                error = "Packet does not carry an ARP message.";
                return false;
        }
    }

    private static string Describe(ArpPacket arp) => arp.IsRequest
        ? $"ARP request: who has {arp.TargetProtocolAddress}? Tell {arp.SenderProtocolAddress} ({arp.SenderHardwareAddress})"
        : $"ARP reply: {arp.SenderProtocolAddress} is at {arp.SenderHardwareAddress}";
}
