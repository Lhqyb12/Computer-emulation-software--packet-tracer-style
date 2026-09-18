using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Packets;

namespace NetSim.Core.Icmp;

/// <summary>
/// An <see cref="IPacketProcessor"/> that handles a <see cref="Packet"/> at the ICMP layer
/// <em>only</em>. It accepts a payload that is an <see cref="IcmpMessage"/> directly, or one
/// carrying an <see cref="IPv4Packet"/> whose <see cref="IPv4Header.Protocol"/> is
/// <see cref="ProtocolNumber.Icmp"/> (optionally itself inside an <see cref="EthernetFrame"/> with
/// <see cref="EtherType.IPv4"/> - the full Ethernet -&gt; IPv4 -&gt; ICMP decapsulation path). It
/// confirms the message is structurally valid <em>and</em> that its checksum matches (unlike
/// <see cref="IPv4Processor"/>, where the header checksum is decorative, an ICMP checksum mismatch
/// is a real rejection - the brief explicitly asks for it), then reports that it would be delivered
/// up the stack.
///
/// It performs no destination-ownership decision and no Echo Reply generation - that stateful,
/// interface-scoped work is <see cref="IcmpLayer"/>'s. Like <see cref="Arp.ArpProcessor"/> it only
/// <em>decides</em> the outcome; the <see cref="IPacketEngine"/> applies the state change and
/// raises events.
/// </summary>
public sealed class IcmpProcessor : IPacketProcessor
{
    public PacketProcessingResult Process(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (!TryExtract(packet.Payload, out var icmp, out var extractionError))
        {
            return PacketProcessingResult.Invalid(extractionError!);
        }

        var validation = icmp!.Validate();
        if (!validation.IsValid)
        {
            return PacketProcessingResult.Dropped(
                IcmpDropReasons.InvalidPacket,
                string.Join("; ", validation.Errors));
        }

        if (!icmp.HasValidChecksum)
        {
            return PacketProcessingResult.Dropped(
                IcmpDropReasons.InvalidChecksum,
                $"Checksum mismatch for {icmp}.");
        }

        return PacketProcessingResult.Delivered(Describe(icmp));
    }

    /// <summary>
    /// Pulls the <see cref="IcmpMessage"/> out of a packet payload: it is the payload itself, it is
    /// the payload of an IPv4 packet whose protocol is ICMP, or it is the payload of an IPv4 packet
    /// (protocol ICMP) carried inside an Ethernet frame with EtherType IPv4. Returns false with an
    /// explanatory <paramref name="error"/> otherwise. Mirrors <see cref="IPv4Processor.TryExtract"/>.
    /// </summary>
    internal static bool TryExtract(IPacketPayload? payload, out IcmpMessage? icmp, out string? error)
    {
        switch (payload)
        {
            case IcmpMessage direct:
                icmp = direct;
                error = null;
                return true;

            case IPv4Packet ip when ip.Protocol == ProtocolNumber.Icmp && ip.Payload is IcmpMessage fromIp:
                icmp = fromIp;
                error = null;
                return true;

            case IPv4Packet ip when ip.Protocol == ProtocolNumber.Icmp:
                icmp = null;
                error = "IPv4 packet declares protocol ICMP but its payload is not an ICMP message.";
                return false;

            case IPv4Packet ip:
                icmp = null;
                error = $"IPv4 packet does not carry ICMP (protocol {ip.Protocol.Name}).";
                return false;

            case EthernetFrame frame when frame.EtherType == EtherType.IPv4 && frame.Payload is IPv4Packet innerIp:
                return TryExtract(innerIp, out icmp, out error);

            case EthernetFrame frame:
                icmp = null;
                error = $"Ethernet frame does not carry IPv4 (EtherType {frame.EtherType.Name}).";
                return false;

            default:
                icmp = null;
                error = "Packet does not carry an ICMP message.";
                return false;
        }
    }

    private static string Describe(IcmpMessage icmp) => icmp.ToString();
}
