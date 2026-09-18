using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// Default <see cref="IIPv4Layer"/>. Stateless apart from its event subscribers; the structural
/// work is delegated to <see cref="IPv4Packet"/> / <see cref="EthernetFrame"/> / the injected
/// <see cref="IPv4Processor"/>.
/// </summary>
public sealed class IPv4Layer : IIPv4Layer
{
    private readonly IPv4Processor _processor;

    public IPv4Layer(IPv4Processor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _processor = processor;
    }

    public event EventHandler<IPv4PacketEventArgs>? PacketCreated;

    public event EventHandler<IPv4PacketEventArgs>? PacketEncapsulated;

    public event EventHandler<IPv4PacketEventArgs>? PacketProcessed;

    public event EventHandler<IPv4PacketEventArgs>? PacketDropped;

    public IPv4Packet CreatePacket(
        IPv4Address source,
        IPv4Address destination,
        IPacketPayload? payload = null,
        ProtocolNumber protocol = default,
        byte timeToLive = IPv4Packet.DefaultTimeToLive,
        ushort identification = 0,
        IPv4Flags flags = IPv4Flags.None,
        int fragmentOffset = 0)
    {
        var packet = IPv4Packet.Create(
            source, destination, payload, protocol, timeToLive, identification, flags, fragmentOffset);
        PacketCreated?.Invoke(this, new IPv4PacketEventArgs(packet, detail: packet.ToString()));
        return packet;
    }

    public EthernetFrame Encapsulate(IPv4Packet packet, MacAddress sourceMac, MacAddress destinationMac)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var frame = EthernetFrame.Create(sourceMac, destinationMac, EtherType.IPv4, packet);
        PacketEncapsulated?.Invoke(this, new IPv4PacketEventArgs(packet, frame, detail: frame.ToString()));
        return frame;
    }

    public bool TryDecapsulate(EthernetFrame frame, [NotNullWhen(true)] out IPv4Packet? packet)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.EtherType == EtherType.IPv4 && frame.Payload is IPv4Packet inner)
        {
            packet = inner;
            return true;
        }

        packet = null;
        return false;
    }

    public PacketProcessingResult Process(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var result = _processor.Process(packet);

        if (IPv4Processor.TryExtract(packet.Payload, out var ipPacket, out _) && ipPacket is not null)
        {
            var frame = packet.Payload as EthernetFrame;
            var args = new IPv4PacketEventArgs(ipPacket, frame, packet, result.DropReason, result.Detail);
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
}
