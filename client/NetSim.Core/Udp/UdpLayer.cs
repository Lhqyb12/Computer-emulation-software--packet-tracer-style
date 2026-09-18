using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;

namespace NetSim.Core.Udp;

/// <summary>
/// Default <see cref="IUdpLayer"/>. Stateless apart from its event subscribers - the mutable socket
/// table lives in the injected <see cref="IUdpDeliveryManager"/>, and the structural work is
/// delegated to <see cref="UdpDatagram"/> / <see cref="IPv4Packet"/> / the injected <see cref="UdpProcessor"/>.
/// </summary>
public sealed class UdpLayer : IUdpLayer
{
    private readonly UdpProcessor _processor;
    private readonly IUdpDeliveryManager _deliveryManager;

    public UdpLayer(UdpProcessor processor, IUdpDeliveryManager deliveryManager)
    {
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(deliveryManager);
        _processor = processor;
        _deliveryManager = deliveryManager;
    }

    public event EventHandler<UdpEventArgs>? DatagramCreated;

    public event EventHandler<UdpEventArgs>? DatagramEncapsulated;

    public event EventHandler<UdpEventArgs>? DatagramReceived;

    public event EventHandler<UdpEventArgs>? DatagramDelivered;

    public event EventHandler<UdpEventArgs>? PortUnavailable;

    public event EventHandler<UdpEventArgs>? PacketProcessed;

    public event EventHandler<UdpEventArgs>? PacketDropped;

    public UdpDatagram CreateDatagram(IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort, IPacketPayload? payload = null)
    {
        var datagram = UdpDatagram.Create(source, destination, sourcePort, destinationPort, payload);
        DatagramCreated?.Invoke(this, new UdpEventArgs(datagram, detail: datagram.ToString()));
        return datagram;
    }

    public IPv4Packet Encapsulate(UdpDatagram datagram, IPv4Address source, IPv4Address destination, byte timeToLive = IPv4Packet.DefaultTimeToLive)
    {
        ArgumentNullException.ThrowIfNull(datagram);

        var packet = IPv4Packet.Create(source, destination, datagram, ProtocolNumber.Udp, timeToLive);
        DatagramEncapsulated?.Invoke(this, new UdpEventArgs(datagram, packet, detail: packet.ToString()));
        return packet;
    }

    public bool TryDecapsulate(IPv4Packet packet, [NotNullWhen(true)] out UdpDatagram? datagram)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (packet.Protocol == ProtocolNumber.Udp && packet.Payload is UdpDatagram inner)
        {
            datagram = inner;
            return true;
        }

        datagram = null;
        return false;
    }

    public PacketProcessingResult Process(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var result = _processor.Process(packet);

        if (UdpProcessor.TryExtract(packet.Payload, out var udp, out _, out _) && udp is not null)
        {
            var args = new UdpEventArgs(udp, carrier: packet, dropReason: result.DropReason, detail: result.Detail);
            if (result.IsSuccess)
            {
                PacketProcessed?.Invoke(this, args);
            }
            else
            {
                PacketDropped?.Invoke(this, args);
            }
        }

        return result;
    }

    public UdpProcessingReport HandleIncoming(NetworkInterface receivingInterface, IPv4Packet ipPacket)
    {
        ArgumentNullException.ThrowIfNull(receivingInterface);
        ArgumentNullException.ThrowIfNull(ipPacket);

        if (!TryDecapsulate(ipPacket, out var udp))
        {
            var report = UdpProcessingReport.Dropped(UdpDropReasons.NotUdp, $"IPv4 packet protocol is {ipPacket.Protocol.Name}, not UDP.");
            PacketDropped?.Invoke(this, new UdpEventArgs(
                ipPacket: ipPacket, networkInterface: receivingInterface, dropReason: UdpDropReasons.NotUdp, detail: report.Description));
            return report;
        }

        var validation = udp.Validate();
        if (!validation.IsValid)
        {
            var report = UdpProcessingReport.Dropped(UdpDropReasons.InvalidPacket, string.Join("; ", validation.Errors));
            PacketDropped?.Invoke(this, new UdpEventArgs(
                udp, ipPacket, networkInterface: receivingInterface, dropReason: UdpDropReasons.InvalidPacket, detail: report.Description));
            return report;
        }

        if (!udp.HasValidChecksum(ipPacket.SourceAddress, ipPacket.DestinationAddress))
        {
            var report = UdpProcessingReport.Dropped(UdpDropReasons.InvalidChecksum, $"Checksum mismatch for {udp}.");
            PacketDropped?.Invoke(this, new UdpEventArgs(
                udp, ipPacket, networkInterface: receivingInterface, dropReason: UdpDropReasons.InvalidChecksum, detail: report.Description));
            return report;
        }

        DatagramReceived?.Invoke(this, new UdpEventArgs(udp, ipPacket, networkInterface: receivingInterface, detail: udp.ToString()));

        var ownsDestination = receivingInterface.IPv4Configurations.Any(c => c.Address == ipPacket.DestinationAddress);
        if (!ownsDestination)
        {
            return UdpProcessingReport.NotOwned(udp, ipPacket.DestinationAddress);
        }

        var delivery = _deliveryManager.Deliver(receivingInterface.Device, udp, ipPacket.SourceAddress, udp.SourcePort);
        if (!delivery.IsDelivered)
        {
            var report = UdpProcessingReport.PortUnavailable(udp);
            PortUnavailable?.Invoke(this, new UdpEventArgs(
                udp, ipPacket, networkInterface: receivingInterface, dropReason: UdpDropReasons.PortUnavailable, detail: report.Description));
            return report;
        }

        var delivered = UdpProcessingReport.Delivered(udp, delivery.Binding!);
        DatagramDelivered?.Invoke(this, new UdpEventArgs(udp, ipPacket, networkInterface: receivingInterface, detail: delivered.Description));
        return delivered;
    }
}
