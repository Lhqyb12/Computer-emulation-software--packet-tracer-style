using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Transport;

namespace NetSim.Core.Tcp;

/// <summary>
/// Default <see cref="ITcpLayer"/>. Stateless apart from its event subscribers - the mutable
/// connection/listener state lives in the injected <see cref="ITcpConnectionManager"/>, and the
/// structural work is delegated to <see cref="TcpSegment"/> / <see cref="IPv4Packet"/> / the
/// injected <see cref="TcpProcessor"/>.
/// </summary>
public sealed class TcpLayer : ITcpLayer
{
    private readonly TcpProcessor _processor;
    private readonly ITcpConnectionManager _connectionManager;

    public TcpLayer(TcpProcessor processor, ITcpConnectionManager connectionManager)
    {
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(connectionManager);
        _processor = processor;
        _connectionManager = connectionManager;
    }

    public event EventHandler<TcpEventArgs>? SegmentCreated;

    public event EventHandler<TcpEventArgs>? SegmentEncapsulated;

    public event EventHandler<TcpEventArgs>? PacketProcessed;

    public event EventHandler<TcpEventArgs>? PacketDropped;

    public TcpSegment CreateSegment(
        IPv4Address source, IPv4Address destination, Port sourcePort, Port destinationPort,
        uint sequenceNumber, uint acknowledgmentNumber, TcpFlags flags,
        ushort windowSize = TcpSegment.DefaultWindowSize, IPacketPayload? payload = null)
    {
        var segment = TcpSegment.Create(source, destination, sourcePort, destinationPort, sequenceNumber, acknowledgmentNumber, flags, windowSize, payload);
        SegmentCreated?.Invoke(this, new TcpEventArgs(segment, detail: segment.ToString()));
        return segment;
    }

    public IPv4Packet Encapsulate(TcpSegment segment, IPv4Address source, IPv4Address destination, byte timeToLive = IPv4Packet.DefaultTimeToLive)
    {
        ArgumentNullException.ThrowIfNull(segment);

        var packet = IPv4Packet.Create(source, destination, segment, ProtocolNumber.Tcp, timeToLive);
        SegmentEncapsulated?.Invoke(this, new TcpEventArgs(segment, packet, detail: packet.ToString()));
        return packet;
    }

    public bool TryDecapsulate(IPv4Packet packet, [NotNullWhen(true)] out TcpSegment? segment)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (packet.Protocol == ProtocolNumber.Tcp && packet.Payload is TcpSegment inner)
        {
            segment = inner;
            return true;
        }

        segment = null;
        return false;
    }

    public PacketProcessingResult Process(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var result = _processor.Process(packet);

        if (TcpProcessor.TryExtract(packet.Payload, out var tcp, out _, out _) && tcp is not null)
        {
            var args = new TcpEventArgs(tcp, carrier: packet, dropReason: result.DropReason, detail: result.Detail);
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

    public TcpProcessingReport HandleIncoming(NetworkInterface receivingInterface, IPv4Packet ipPacket)
    {
        ArgumentNullException.ThrowIfNull(receivingInterface);
        ArgumentNullException.ThrowIfNull(ipPacket);

        if (!TryDecapsulate(ipPacket, out var segment))
        {
            var report = TcpProcessingReport.Dropped(TcpDropReasons.NotTcp, $"IPv4 packet protocol is {ipPacket.Protocol.Name}, not TCP.");
            PacketDropped?.Invoke(this, new TcpEventArgs(
                ipPacket: ipPacket, networkInterface: receivingInterface, dropReason: TcpDropReasons.NotTcp, detail: report.Description));
            return report;
        }

        var validation = segment.Validate();
        if (!validation.IsValid)
        {
            var report = TcpProcessingReport.Dropped(TcpDropReasons.InvalidPacket, string.Join("; ", validation.Errors));
            PacketDropped?.Invoke(this, new TcpEventArgs(
                segment, ipPacket, networkInterface: receivingInterface, dropReason: TcpDropReasons.InvalidPacket, detail: report.Description));
            return report;
        }

        if (!segment.HasValidChecksum(ipPacket.SourceAddress, ipPacket.DestinationAddress))
        {
            var report = TcpProcessingReport.Dropped(TcpDropReasons.InvalidChecksum, $"Checksum mismatch for {segment}.");
            PacketDropped?.Invoke(this, new TcpEventArgs(
                segment, ipPacket, networkInterface: receivingInterface, dropReason: TcpDropReasons.InvalidChecksum, detail: report.Description));
            return report;
        }

        var ownsDestination = receivingInterface.IPv4Configurations.Any(c => c.Address == ipPacket.DestinationAddress);
        if (!ownsDestination)
        {
            var report = TcpProcessingReport.Dropped(
                TcpDropReasons.DestinationNotOwned, $"{receivingInterface.Name} does not own {ipPacket.DestinationAddress}.");
            PacketDropped?.Invoke(this, new TcpEventArgs(
                segment, ipPacket, networkInterface: receivingInterface, dropReason: TcpDropReasons.DestinationNotOwned, detail: report.Description));
            return report;
        }

        PacketProcessed?.Invoke(this, new TcpEventArgs(segment, ipPacket, networkInterface: receivingInterface, detail: segment.ToString()));

        return _connectionManager.AcceptSegment(receivingInterface, ipPacket.DestinationAddress, ipPacket.SourceAddress, segment);
    }
}
