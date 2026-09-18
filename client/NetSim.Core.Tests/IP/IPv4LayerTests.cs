using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tests.Packets;
using NetSim.Core.Devices;

namespace NetSim.Core.Tests.IP;

public class IPv4LayerTests
{
    private static readonly IPv4Address Src = IPv4Address.Parse("10.0.0.1");
    private static readonly IPv4Address Dst = IPv4Address.Parse("10.0.0.2");
    private static readonly MacAddress SrcMac = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly MacAddress DstMac = MacAddress.Parse("00:AA:BB:CC:DD:EE");

    private static IPv4Layer NewLayer() => new(new IPv4Processor());

    [Fact]
    public void CreatePacket_BuildsThePacket_AndRaisesPacketCreated()
    {
        var layer = NewLayer();
        IPv4PacketEventArgs? seen = null;
        layer.PacketCreated += (_, e) => seen = e;

        var packet = layer.CreatePacket(Src, Dst, RawPayload.OfSize(16), ProtocolNumber.Tcp, timeToLive: 50);

        Assert.Equal(Src, packet.SourceAddress);
        Assert.Equal(50, packet.TimeToLive);
        Assert.NotNull(seen);
        Assert.Same(packet, seen!.Packet);
    }

    [Fact]
    public void Encapsulate_WrapsThePacketInAnIPv4EtherTypeFrame_AndRaisesPacketEncapsulated()
    {
        var layer = NewLayer();
        var packet = layer.CreatePacket(Src, Dst, RawPayload.FromText("data"), ProtocolNumber.Udp);
        IPv4PacketEventArgs? seen = null;
        layer.PacketEncapsulated += (_, e) => seen = e;

        var frame = layer.Encapsulate(packet, SrcMac, DstMac);

        Assert.Equal(EtherType.IPv4, frame.EtherType);
        Assert.Same(packet, frame.Payload);
        Assert.Same(frame, seen!.Frame);
    }

    [Fact]
    public void TryDecapsulate_ReturnsThePacketFromAnIPv4Frame_AndFailsForOthers()
    {
        var layer = NewLayer();
        var packet = layer.CreatePacket(Src, Dst, RawPayload.OfSize(8), ProtocolNumber.Tcp);
        var ipv4Frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv4, packet);
        var arpFrame = EthernetFrame.Create(SrcMac, DstMac, EtherType.Arp, RawPayload.OfSize(8));

        Assert.True(layer.TryDecapsulate(ipv4Frame, out var extracted));
        Assert.Same(packet, extracted);

        Assert.False(layer.TryDecapsulate(arpFrame, out var none));
        Assert.Null(none);
    }

    [Fact]
    public void Process_ValidPacket_RaisesPacketProcessed()
    {
        var layer = NewLayer();
        var ip = layer.CreatePacket(Src, Dst, RawPayload.OfSize(32), ProtocolNumber.Tcp);
        var carrier = new Packet(new Pc("A"), new Pc("B"), "IPv4", ip);
        IPv4PacketEventArgs? processed = null;
        IPv4PacketEventArgs? dropped = null;
        layer.PacketProcessed += (_, e) => processed = e;
        layer.PacketDropped += (_, e) => dropped = e;

        var result = layer.Process(carrier);

        Assert.True(result.IsSuccess);
        Assert.NotNull(processed);
        Assert.Null(dropped);
        Assert.Same(ip, processed!.Packet);
        Assert.Same(carrier, processed.Carrier);
    }

    [Fact]
    public void Process_ExpiredPacket_RaisesPacketDropped_WithReason()
    {
        var layer = NewLayer();
        var ip = IPv4Packet.Create(Src, Dst, timeToLive: 1).WithDecrementedTimeToLive();
        var carrier = new Packet(new Pc("A"), new Pc("B"), "IPv4", ip);
        IPv4PacketEventArgs? dropped = null;
        layer.PacketDropped += (_, e) => dropped = e;

        var result = layer.Process(carrier);

        Assert.False(result.IsSuccess);
        Assert.NotNull(dropped);
        Assert.Same(IPv4DropReasons.TimeToLiveExpired, dropped!.DropReason);
    }

    [Fact]
    public void Constructor_NullProcessor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IPv4Layer(null!));
    }
}
