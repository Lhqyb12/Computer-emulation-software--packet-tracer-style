using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tests.Packets;

namespace NetSim.Core.Tests.IP;

public class IPv4ProcessorTests
{
    private static readonly IPv4Address Src = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address Dst = IPv4Address.Parse("192.168.1.20");
    private static readonly MacAddress SrcMac = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly MacAddress DstMac = MacAddress.Parse("00:AA:BB:CC:DD:EE");

    private static Packet PacketCarrying(IPacketPayload? payload) =>
        new(new Pc("A"), new Pc("B"), "IPv4", payload);

    [Fact]
    public void Process_BareIPv4Packet_IsDelivered_WithAddressDetail()
    {
        var ip = IPv4Packet.Create(Src, Dst, RawPayload.OfSize(64), ProtocolNumber.Tcp);

        var result = new IPv4Processor().Process(PacketCarrying(ip));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Contains("192.168.1.10", result.Detail);
        Assert.Contains("192.168.1.20", result.Detail);
        Assert.Contains("TCP", result.Detail);
    }

    [Fact]
    public void Process_IPv4InsideEthernetFrame_IsDecapsulatedAndDelivered()
    {
        var ip = IPv4Packet.Create(Src, Dst, RawPayload.OfSize(16), ProtocolNumber.Udp);
        var frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv4, ip);

        var result = new IPv4Processor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Contains("192.168.1.10", result.Detail);
    }

    [Fact]
    public void Process_EthernetFrameWithNonIPv4EtherType_IsInvalid()
    {
        var frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.Arp, RawPayload.OfSize(8));

        var result = new IPv4Processor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_EthernetFrameClaimingIPv4ButCarryingSomethingElse_IsInvalid()
    {
        var frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv4, RawPayload.FromText("not ip"));

        var result = new IPv4Processor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
        Assert.Contains("not an IPv4 packet", result.Detail);
    }

    [Fact]
    public void Process_NonIPv4Payload_IsInvalid()
    {
        var result = new IPv4Processor().Process(PacketCarrying(RawPayload.FromText("plain")));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_PacketWithExpiredTtl_IsDroppedWithTtlExpiredReason()
    {
        var expired = IPv4Packet.Create(Src, Dst, timeToLive: 1).WithDecrementedTimeToLive();

        var result = new IPv4Processor().Process(PacketCarrying(expired));

        Assert.Equal(PacketProcessingOutcome.Dropped, result.Outcome);
        Assert.Same(IPv4DropReasons.TimeToLiveExpired, result.DropReason);
    }

    [Fact]
    public void Process_PacketWithBrokenInnerPayload_IsDroppedWithInvalidPacketReason()
    {
        var badInner = new StubPayload { ValidationResult = PacketValidationResult.Invalid("broken") };
        var ip = IPv4Packet.Create(Src, Dst, badInner);

        var result = new IPv4Processor().Process(PacketCarrying(ip));

        Assert.Equal(PacketProcessingOutcome.Dropped, result.Outcome);
        Assert.Same(IPv4DropReasons.InvalidPacket, result.DropReason);
    }

    [Fact]
    public void RunThroughThePacketEngine_ValidPacket_EndsDelivered()
    {
        var engine = new PacketEngine();
        var ip = IPv4Packet.Create(Src, Dst, RawPayload.OfSize(32), ProtocolNumber.Tcp);
        var packet = engine.CreatePacket(new Pc("A"), new Pc("B"), "IPv4", ip);

        var result = engine.Process(packet, new IPv4Processor());

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Equal(PacketState.Delivered, packet.State);
    }

    [Fact]
    public void Process_NullPacket_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IPv4Processor().Process(null!));
    }
}
