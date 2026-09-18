using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Packets;

namespace NetSim.Core.Tcp;

/// <summary>
/// An <see cref="IPacketProcessor"/> that handles a <see cref="Packet"/> at the TCP layer
/// <em>only</em>: structural validation and checksum verification, exactly the same shape and scope
/// as <see cref="Udp.UdpProcessor"/>. It performs no connection lookup and no state-machine
/// decision - that stateful work is <see cref="ITcpConnectionManager"/>'s, driven by
/// <see cref="ITcpLayer.HandleIncoming"/>.
/// </summary>
public sealed class TcpProcessor : IPacketProcessor
{
    public PacketProcessingResult Process(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (!TryExtract(packet.Payload, out var tcp, out var ipPacket, out var extractionError))
        {
            return PacketProcessingResult.Invalid(extractionError!);
        }

        var validation = tcp!.Validate();
        if (!validation.IsValid)
        {
            return PacketProcessingResult.Dropped(TcpDropReasons.InvalidPacket, string.Join("; ", validation.Errors));
        }

        if (!tcp.HasValidChecksum(ipPacket!.SourceAddress, ipPacket.DestinationAddress))
        {
            return PacketProcessingResult.Dropped(TcpDropReasons.InvalidChecksum, $"Checksum mismatch for {tcp}.");
        }

        return PacketProcessingResult.Delivered(tcp.ToString());
    }

    /// <summary>
    /// Pulls the <see cref="TcpSegment"/> and its enclosing <see cref="IPv4Packet"/> out of a packet
    /// payload, mirroring <see cref="Udp.UdpProcessor.TryExtract"/>: the payload is an IPv4 packet
    /// (protocol TCP) carrying a segment, optionally itself inside an Ethernet frame with EtherType
    /// IPv4. A bare <see cref="TcpSegment"/> with no IPv4 context is rejected - the checksum cannot
    /// be validated without the source/destination addresses.
    /// </summary>
    internal static bool TryExtract(IPacketPayload? payload, out TcpSegment? tcp, out IPv4Packet? ipPacket, out string? error)
    {
        switch (payload)
        {
            case IPv4Packet ip when ip.Protocol == ProtocolNumber.Tcp && ip.Payload is TcpSegment fromIp:
                tcp = fromIp;
                ipPacket = ip;
                error = null;
                return true;

            case IPv4Packet ip when ip.Protocol == ProtocolNumber.Tcp:
                tcp = null;
                ipPacket = null;
                error = "IPv4 packet declares protocol TCP but its payload is not a TCP segment.";
                return false;

            case IPv4Packet ip:
                tcp = null;
                ipPacket = null;
                error = $"IPv4 packet does not carry TCP (protocol {ip.Protocol.Name}).";
                return false;

            case EthernetFrame frame when frame.EtherType == EtherType.IPv4 && frame.Payload is IPv4Packet innerIp:
                return TryExtract(innerIp, out tcp, out ipPacket, out error);

            case EthernetFrame frame:
                tcp = null;
                ipPacket = null;
                error = $"Ethernet frame does not carry IPv4 (EtherType {frame.EtherType.Name}).";
                return false;

            default:
                tcp = null;
                ipPacket = null;
                error = "Packet does not carry a TCP segment wrapped in an IPv4 packet.";
                return false;
        }
    }
}
