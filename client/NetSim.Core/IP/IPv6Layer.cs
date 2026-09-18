using System.Diagnostics.CodeAnalysis;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.IP;

/// <summary>
/// Default <see cref="IIPv6Layer"/>. Stateless apart from its event subscribers; the structural
/// work is delegated to <see cref="IPv6Packet"/> / <see cref="EthernetFrame"/> / the injected
/// <see cref="IPv6Processor"/>.
/// </summary>
public sealed class IPv6Layer : IIPv6Layer
{
    private readonly IPv6Processor _processor;

    public IPv6Layer(IPv6Processor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);
        _processor = processor;
    }

    public event EventHandler<IPv6PacketEventArgs>? PacketCreated;

    public event EventHandler<IPv6PacketEventArgs>? PacketEncapsulated;

    public event EventHandler<IPv6PacketEventArgs>? PacketProcessed;

    public event EventHandler<IPv6PacketEventArgs>? PacketDropped;

    public IPv6Packet CreatePacket(
        IPv6Address source,
        IPv6Address destination,
        IPacketPayload? payload = null,
        NextHeader nextHeader = default,
        byte hopLimit = IPv6Packet.DefaultHopLimit,
        byte trafficClass = 0,
        int flowLabel = 0)
    {
        var packet = IPv6Packet.Create(source, destination, payload, nextHeader, hopLimit, trafficClass, flowLabel);
        PacketCreated?.Invoke(this, new IPv6PacketEventArgs(packet, detail: packet.ToString()));
        return packet;
    }

    public EthernetFrame Encapsulate(IPv6Packet packet, MacAddress sourceMac, MacAddress destinationMac)
    {
        ArgumentNullException.ThrowIfNull(packet);

        var frame = EthernetFrame.Create(sourceMac, destinationMac, EtherType.IPv6, packet);
        PacketEncapsulated?.Invoke(this, new IPv6PacketEventArgs(packet, frame, detail: frame.ToString()));
        return frame;
    }

    public bool TryDecapsulate(EthernetFrame frame, [NotNullWhen(true)] out IPv6Packet? packet)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.EtherType == EtherType.IPv6 && frame.Payload is IPv6Packet inner)
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

        if (IPv6Processor.TryExtract(packet.Payload, out var ipPacket, out _) && ipPacket is not null)
        {
            var frame = packet.Payload as EthernetFrame;
            var args = new IPv6PacketEventArgs(ipPacket, frame, packet, result.DropReason, result.Detail);
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
