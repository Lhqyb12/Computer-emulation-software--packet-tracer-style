using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tests.Packets;

namespace NetSim.Core.Tests.Ethernet;

public class EthernetProcessorTests
{
    private static readonly MacAddress Src = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly MacAddress Dst = MacAddress.Parse("00:AA:BB:CC:DD:EE");

    private static Packet PacketCarrying(IPacketPayload? payload) =>
        new(new Pc("A"), new Pc("B"), "Ethernet", payload);

    [Fact]
    public void Process_NonEthernetPayload_IsInvalid()
    {
        var result = new EthernetProcessor().Process(PacketCarrying(RawPayload.FromText("not a frame")));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Process_NoPayload_IsInvalid()
    {
        var result = new EthernetProcessor().Process(PacketCarrying(null));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_ValidUnicastFrame_IsDelivered_WithClassificationDetail()
    {
        var frame = EthernetFrame.Create(Src, Dst, EtherType.IPv4, RawPayload.OfSize(64));

        var result = new EthernetProcessor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.True(result.IsSuccess);
        Assert.Contains("unicast", result.Detail);
        Assert.Contains("IPv4", result.Detail);
    }

    [Fact]
    public void Process_BroadcastFrame_IsDelivered_AndDescribedAsBroadcast()
    {
        var frame = EthernetFrame.Create(Src, MacAddress.Broadcast, EtherType.Arp);

        var result = new EthernetProcessor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Contains("broadcast", result.Detail);
    }

    [Fact]
    public void Process_FrameWithBrokenPayload_IsDroppedWithEthernetInvalidFrameReason()
    {
        var badInner = new StubPayload { ValidationResult = PacketValidationResult.Invalid("bad") };
        var frame = EthernetFrame.Create(Src, Dst, EtherType.IPv4, badInner);

        var result = new EthernetProcessor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Dropped, result.Outcome);
        Assert.Same(EthernetDropReasons.InvalidFrame, result.DropReason);
    }

    [Fact]
    public void RunThroughThePacketEngine_ValidFrame_EndsDelivered()
    {
        var engine = new PacketEngine();
        var frame = EthernetFrame.Create(Src, Dst, EtherType.IPv4, RawPayload.OfSize(32));
        var packet = engine.CreatePacket(new Pc("A"), new Pc("B"), "Ethernet", frame);

        var result = engine.Process(packet, new EthernetProcessor());

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Equal(PacketState.Delivered, packet.State);
        Assert.True(packet.HasCompleted);
    }

    [Fact]
    public void RunThroughThePacketEngine_NonFrame_EndsDroppedInvalid()
    {
        var engine = new PacketEngine();
        var packet = engine.CreatePacket(new Pc("A"), new Pc("B"), "Ethernet", RawPayload.FromText("x"));

        engine.Process(packet, new EthernetProcessor());

        Assert.Equal(PacketState.Dropped, packet.State);
        Assert.Same(PacketDropReason.InvalidPacket, packet.DropReason);
    }

    [Fact]
    public void Process_NullPacket_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new EthernetProcessor().Process(null!));
    }
}
