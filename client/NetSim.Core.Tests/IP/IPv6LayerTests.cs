using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.IP;

public class IPv6LayerTests
{
    private static readonly IPv6Address Src = IPv6Address.Parse("2001:db8::1");
    private static readonly IPv6Address Dst = IPv6Address.Parse("2001:db8::2");
    private static readonly MacAddress SrcMac = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly MacAddress DstMac = MacAddress.Parse("00:AA:BB:CC:DD:EE");

    private static IPv6Layer NewLayer() => new(new IPv6Processor());

    [Fact]
    public void CreatePacket_BuildsThePacket_AndRaisesPacketCreated()
    {
        var layer = NewLayer();
        IPv6PacketEventArgs? seen = null;
        layer.PacketCreated += (_, e) => seen = e;

        var packet = layer.CreatePacket(Src, Dst, RawPayload.OfSize(16), NextHeader.Tcp, hopLimit: 50);

        Assert.Equal(Src, packet.SourceAddress);
        Assert.Equal(50, packet.HopLimit);
        Assert.NotNull(seen);
        Assert.Same(packet, seen!.Packet);
    }

    [Fact]
    public void Encapsulate_WrapsThePacketInAnIPv6EtherTypeFrame_AndRaisesPacketEncapsulated()
    {
        var layer = NewLayer();
        var packet = layer.CreatePacket(Src, Dst, RawPayload.FromText("data"), NextHeader.Udp);
        IPv6PacketEventArgs? seen = null;
        layer.PacketEncapsulated += (_, e) => seen = e;

        var frame = layer.Encapsulate(packet, SrcMac, DstMac);

        Assert.Equal(EtherType.IPv6, frame.EtherType);
        Assert.Same(packet, frame.Payload);
        Assert.Same(frame, seen!.Frame);
    }

    [Fact]
    public void TryDecapsulate_ReturnsThePacketFromAnIPv6Frame_AndFailsForOthers()
    {
        var layer = NewLayer();
        var packet = layer.CreatePacket(Src, Dst, RawPayload.OfSize(8), NextHeader.Tcp);
        var ipv6Frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv6, packet);
        var ipv4Frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv4, RawPayload.OfSize(8));

        Assert.True(layer.TryDecapsulate(ipv6Frame, out var extracted));
        Assert.Same(packet, extracted);

        Assert.False(layer.TryDecapsulate(ipv4Frame, out var none));
        Assert.Null(none);
    }

    [Fact]
    public void Process_ValidPacket_RaisesPacketProcessed()
    {
        var layer = NewLayer();
        var ip = layer.CreatePacket(Src, Dst, RawPayload.OfSize(32), NextHeader.Tcp);
        var carrier = new Packet(new Pc("A"), new Pc("B"), "IPv6", ip);
        IPv6PacketEventArgs? processed = null;
        IPv6PacketEventArgs? dropped = null;
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
        var ip = IPv6Packet.Create(Src, Dst, hopLimit: 1).WithDecrementedHopLimit();
        var carrier = new Packet(new Pc("A"), new Pc("B"), "IPv6", ip);
        IPv6PacketEventArgs? dropped = null;
        layer.PacketDropped += (_, e) => dropped = e;

        var result = layer.Process(carrier);

        Assert.False(result.IsSuccess);
        Assert.NotNull(dropped);
        Assert.Same(IPv6DropReasons.HopLimitExpired, dropped!.DropReason);
    }

    [Fact]
    public void Constructor_NullProcessor_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IPv6Layer(null!));
    }
}
