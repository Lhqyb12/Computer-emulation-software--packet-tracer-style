using System.Diagnostics.CodeAnalysis;
using System.Linq;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Icmp;

/// <summary>
/// Default <see cref="IIcmpLayer"/>. Stateless apart from its event subscribers - the structural
/// work is delegated to <see cref="IcmpMessage"/> / <see cref="IPv4Packet"/> / the injected
/// <see cref="IcmpProcessor"/>, exactly like <see cref="Arp.ArpLayer"/>.
/// </summary>
public sealed class IcmpLayer : IIcmpLayer
{
    private readonly IcmpProcessor _processor;

    public IcmpLayer(IcmpProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _processor = processor;
    }

    public event EventHandler<IcmpEventArgs>? EchoRequestCreated;

    public event EventHandler<IcmpEventArgs>? EchoRequestEncapsulated;

    public event EventHandler<IcmpEventArgs>? EchoReplyCreated;

    public event EventHandler<IcmpEventArgs>? EchoReplyEncapsulated;

    public event EventHandler<IcmpEventArgs>? EchoRequestReceived;

    public event EventHandler<IcmpEventArgs>? EchoReplyReceived;

    public event EventHandler<IcmpEventArgs>? ErrorMessageReceived;

    public event EventHandler<IcmpEventArgs>? PacketProcessed;

    public event EventHandler<IcmpEventArgs>? PacketDropped;

    public IcmpMessage CreateEchoRequest(ushort identifier, ushort sequenceNumber, RawPayload? data = null)
    {
        var message = IcmpMessage.CreateEchoRequest(identifier, sequenceNumber, data);
        EchoRequestCreated?.Invoke(this, new IcmpEventArgs(message, detail: message.ToString()));
        return message;
    }

    public IcmpMessage CreateEchoReply(IcmpMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var reply = IcmpMessage.CreateEchoReplyTo(request);
        EchoReplyCreated?.Invoke(this, new IcmpEventArgs(reply, detail: reply.ToString()));
        return reply;
    }

    public IcmpMessage CreateTimeExceeded(IcmpOriginalDatagramInfo originalDatagram, IcmpCode? code = null) =>
        IcmpMessage.CreateTimeExceeded(originalDatagram, code);

    public IcmpMessage CreateDestinationUnreachable(IcmpOriginalDatagramInfo originalDatagram, IcmpCode code) =>
        IcmpMessage.CreateDestinationUnreachable(originalDatagram, code);

    public IPv4Packet Encapsulate(IcmpMessage message, IPv4Address source, IPv4Address destination, byte timeToLive = IPv4Packet.DefaultTimeToLive)
    {
        ArgumentNullException.ThrowIfNull(message);

        var packet = IPv4Packet.Create(source, destination, message, ProtocolNumber.Icmp, timeToLive);

        if (message.IsEchoRequest)
        {
            EchoRequestEncapsulated?.Invoke(this, new IcmpEventArgs(message, packet, detail: packet.ToString()));
        }
        else if (message.IsEchoReply)
        {
            EchoReplyEncapsulated?.Invoke(this, new IcmpEventArgs(message, packet, detail: packet.ToString()));
        }

        return packet;
    }

    public bool TryDecapsulate(IPv4Packet packet, [NotNullWhen(true)] out IcmpMessage? message)
    {
        ArgumentNullException.ThrowIfNull(packet);

        if (packet.Protocol == ProtocolNumber.Icmp && packet.Payload is IcmpMessage inner)
        {
            message = inner;
            return true;
        }

        message = null;
        return false;
    }

    public PacketProcessingResult Process(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var result = _processor.Process(packet);

        if (IcmpProcessor.TryExtract(packet.Payload, out var icmp, out _) && icmp is not null)
        {
            var args = new IcmpEventArgs(icmp, carrier: packet, dropReason: result.DropReason, detail: result.Detail);
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

    public IcmpProcessingReport HandleIncoming(NetworkInterface receivingInterface, IPv4Packet ipPacket)
    {
        ArgumentNullException.ThrowIfNull(receivingInterface);
        ArgumentNullException.ThrowIfNull(ipPacket);

        if (!TryDecapsulate(ipPacket, out var icmp))
        {
            var report = IcmpProcessingReport.Dropped(
                IcmpDropReasons.NotIcmp, $"IPv4 packet protocol is {ipPacket.Protocol.Name}, not ICMP.");
            PacketDropped?.Invoke(this, new IcmpEventArgs(
                ipPacket: ipPacket, networkInterface: receivingInterface, dropReason: IcmpDropReasons.NotIcmp, detail: report.Description));
            return report;
        }

        var validation = icmp.Validate();
        if (!validation.IsValid)
        {
            var report = IcmpProcessingReport.Dropped(IcmpDropReasons.InvalidPacket, string.Join("; ", validation.Errors));
            PacketDropped?.Invoke(this, new IcmpEventArgs(
                icmp, ipPacket, networkInterface: receivingInterface, dropReason: IcmpDropReasons.InvalidPacket, detail: report.Description));
            return report;
        }

        if (!icmp.HasValidChecksum)
        {
            var report = IcmpProcessingReport.Dropped(IcmpDropReasons.InvalidChecksum, $"Checksum mismatch for {icmp}.");
            PacketDropped?.Invoke(this, new IcmpEventArgs(
                icmp, ipPacket, networkInterface: receivingInterface, dropReason: IcmpDropReasons.InvalidChecksum, detail: report.Description));
            return report;
        }

        if (icmp.IsEchoReply)
        {
            EchoReplyReceived?.Invoke(this, new IcmpEventArgs(icmp, ipPacket, networkInterface: receivingInterface, detail: icmp.ToString()));
            return IcmpProcessingReport.ForEchoReply(icmp, ipPacket.SourceAddress);
        }

        if (icmp.IsError)
        {
            ErrorMessageReceived?.Invoke(this, new IcmpEventArgs(icmp, ipPacket, networkInterface: receivingInterface, detail: icmp.ToString()));
            return IcmpProcessingReport.ForError(icmp, ipPacket.SourceAddress);
        }

        // Echo Request: answered only when this interface owns the IPv4 destination - never for an
        // arbitrary address, never forwarded (no routing in this phase, mirroring ArpLayer).
        EchoRequestReceived?.Invoke(this, new IcmpEventArgs(icmp, ipPacket, networkInterface: receivingInterface, detail: icmp.ToString()));

        var ownsDestination = receivingInterface.IPv4Configurations.Any(c => c.Address == ipPacket.DestinationAddress);
        if (!ownsDestination)
        {
            return IcmpProcessingReport.ForEchoRequest(icmp, ipPacket.DestinationAddress, ownsDestination: false, reply: null, replyPacket: null);
        }

        var reply = CreateEchoReply(icmp);
        var replyPacket = Encapsulate(reply, ipPacket.DestinationAddress, ipPacket.SourceAddress);
        return IcmpProcessingReport.ForEchoRequest(icmp, ipPacket.DestinationAddress, ownsDestination: true, reply, replyPacket);
    }
}
