using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tests.Packets;

namespace NetSim.Core.Tests.IP;

public class IPv6ProcessorTests
{
    private static readonly IPv6Address Src = IPv6Address.Parse("2001:db8:1::10");
    private static readonly IPv6Address Dst = IPv6Address.Parse("2001:db8:1::20");
    private static readonly MacAddress SrcMac = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly MacAddress DstMac = MacAddress.Parse("00:AA:BB:CC:DD:EE");

    private static Packet PacketCarrying(IPacketPayload? payload) =>
        new(new Pc("A"), new Pc("B"), "IPv6", payload);

    [Fact]
    public void Process_BareIPv6Packet_IsDelivered_WithAddressDetail()
    {
        var ip = IPv6Packet.Create(Src, Dst, RawPayload.OfSize(64), NextHeader.Tcp);

        var result = new IPv6Processor().Process(PacketCarrying(ip));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Contains("2001:db8:1::10", result.Detail);
        Assert.Contains("2001:db8:1::20", result.Detail);
        Assert.Contains("TCP", result.Detail);
    }

    [Fact]
    public void Process_IPv6InsideEthernetFrame_IsDecapsulatedAndDelivered()
    {
        var ip = IPv6Packet.Create(Src, Dst, RawPayload.OfSize(16), NextHeader.Udp);
        var frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv6, ip);

        var result = new IPv6Processor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Contains("2001:db8:1::10", result.Detail);
    }

    [Fact]
    public void Process_EthernetFrameWithNonIPv6EtherType_IsInvalid()
    {
        var frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv4, RawPayload.OfSize(8));

        var result = new IPv6Processor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_EthernetFrameClaimingIPv6ButCarryingSomethingElse_IsInvalid()
    {
        var frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv6, RawPayload.FromText("not ip"));

        var result = new IPv6Processor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
        Assert.Contains("not an IPv6 packet", result.Detail);
    }

    [Fact]
    public void Process_NonIPv6Payload_IsInvalid()
    {
        var result = new IPv6Processor().Process(PacketCarrying(RawPayload.FromText("plain")));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_PacketWithExpiredHopLimit_IsDroppedWithHopLimitExpiredReason()
    {
        var expired = IPv6Packet.Create(Src, Dst, hopLimit: 1).WithDecrementedHopLimit();

        var result = new IPv6Processor().Process(PacketCarrying(expired));

        Assert.Equal(PacketProcessingOutcome.Dropped, result.Outcome);
        Assert.Same(IPv6DropReasons.HopLimitExpired, result.DropReason);
    }

    [Fact]
    public void Process_PacketWithBrokenInnerPayload_IsDroppedWithInvalidPacketReason()
    {
        var badInner = new StubPayload { ValidationResult = PacketValidationResult.Invalid("broken") };
        var ip = IPv6Packet.Create(Src, Dst, badInner);

        var result = new IPv6Processor().Process(PacketCarrying(ip));

        Assert.Equal(PacketProcessingOutcome.Dropped, result.Outcome);
        Assert.Same(IPv6DropReasons.InvalidPacket, result.DropReason);
    }

    [Fact]
    public void RunThroughThePacketEngine_ValidPacket_EndsDelivered()
    {
        var engine = new PacketEngine();
        var ip = IPv6Packet.Create(Src, Dst, RawPayload.OfSize(32), NextHeader.Tcp);
        var packet = engine.CreatePacket(new Pc("A"), new Pc("B"), "IPv6", ip);

        var result = engine.Process(packet, new IPv6Processor());

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Equal(PacketState.Delivered, packet.State);
    }

    [Fact]
    public void Process_NullPacket_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IPv6Processor().Process(null!));
    }
}
