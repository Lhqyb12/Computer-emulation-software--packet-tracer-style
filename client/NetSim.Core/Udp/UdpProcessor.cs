using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Packets;

namespace NetSim.Core.Udp;

/// <summary>
/// An <see cref="IPacketProcessor"/> that handles a <see cref="Packet"/> at the UDP layer
/// <em>only</em>: structural validation and checksum verification. It accepts a payload that is an
/// <see cref="IPv4Packet"/> (protocol UDP) carrying a <see cref="UdpDatagram"/>, optionally itself
/// inside an <see cref="EthernetFrame"/> - the full Ethernet -&gt; IPv4 -&gt; UDP decapsulation path.
/// Unlike <see cref="IPv4Processor"/>'s decorative header checksum, a UDP checksum mismatch is a
/// real rejection (brief section 22: "do not silently accept invalid checksums") - but unlike
/// <see cref="Icmp.IcmpProcessor"/>, the checksum needs the enclosing IPv4 packet's source/destination
/// addresses (the pseudo-header), so - unlike ICMP - a "bare" <see cref="UdpDatagram"/> with no IPv4
/// context cannot be checksum-validated and is rejected as <see cref="PacketProcessingResult.Invalid"/>.
///
/// It performs no port lookup and no delivery - that stateful, device-scoped work belongs to
/// <see cref="IUdpDeliveryManager"/> / <see cref="IUdpLayer.HandleIncoming"/>, exactly like ARP/ICMP
/// split their processor from their layer.
/// </summary>
public sealed class UdpProcessor : IPacketProcessor
{
    public PacketProcessingResult Process(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (!TryExtract(packet.Payload, out var udp, out var ipPacket, out var extractionError))
        {
            return PacketProcessingResult.Invalid(extractionError!);
        }

        var validation = udp!.Validate();
        if (!validation.IsValid)
        {
            return PacketProcessingResult.Dropped(UdpDropReasons.InvalidPacket, string.Join("; ", validation.Errors));
        }

        if (!udp.HasValidChecksum(ipPacket!.SourceAddress, ipPacket.DestinationAddress))
        {
            return PacketProcessingResult.Dropped(UdpDropReasons.InvalidChecksum, $"Checksum mismatch for {udp}.");
        }

        return PacketProcessingResult.Delivered(udp.ToString());
    }

    /// <summary>
    /// Pulls the <see cref="UdpDatagram"/> and its enclosing <see cref="IPv4Packet"/> out of a
    /// packet payload: the payload is an IPv4 packet (protocol UDP) carrying a datagram, optionally
    /// itself inside an Ethernet frame with EtherType IPv4. A bare <see cref="UdpDatagram"/> with no
    /// IPv4 context is rejected (<paramref name="ipPacket"/> would be null - the checksum cannot be
    /// validated without the source/destination addresses).
    /// </summary>
    internal static bool TryExtract(IPacketPayload? payload, out UdpDatagram? udp, out IPv4Packet? ipPacket, out string? error)
    {
        switch (payload)
        {
            case IPv4Packet ip when ip.Protocol == ProtocolNumber.Udp && ip.Payload is UdpDatagram fromIp:
                udp = fromIp;
                ipPacket = ip;
                error = null;
                return true;

            case IPv4Packet ip when ip.Protocol == ProtocolNumber.Udp:
                udp = null;
                ipPacket = null;
                error = "IPv4 packet declares protocol UDP but its payload is not a UDP datagram.";
                return false;

            case IPv4Packet ip:
                udp = null;
                ipPacket = null;
                error = $"IPv4 packet does not carry UDP (protocol {ip.Protocol.Name}).";
                return false;

            case EthernetFrame frame when frame.EtherType == EtherType.IPv4 && frame.Payload is IPv4Packet innerIp:
                return TryExtract(innerIp, out udp, out ipPacket, out error);

            case EthernetFrame frame:
                udp = null;
                ipPacket = null;
                error = $"Ethernet frame does not carry IPv4 (EtherType {frame.EtherType.Name}).";
                return false;

            default:
                udp = null;
                ipPacket = null;
                error = "Packet does not carry a UDP datagram wrapped in an IPv4 packet.";
                return false;
        }
    }
}
