using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tests.Packets;

namespace NetSim.Core.Tests.Arp;

public class ArpProcessorTests
{
    private static readonly MacAddress SenderMac = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly IPv4Address SenderIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address TargetIp = IPv4Address.Parse("192.168.1.20");
    private static readonly MacAddress DstMac = MacAddress.Parse("AA:BB:CC:DD:EE:FF");

    private static Packet PacketCarrying(IPacketPayload? payload) => new(new Pc("A"), new Pc("B"), "ARP", payload);

    [Fact]
    public void Process_BareArpRequest_IsDelivered_WithDetail()
    {
        var arp = ArpPacket.CreateRequest(SenderMac, SenderIp, TargetIp);

        var result = new ArpProcessor().Process(PacketCarrying(arp));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Contains("192.168.1.20", result.Detail);
        Assert.Contains("192.168.1.10", result.Detail);
    }

    [Fact]
    public void Process_ArpInsideEthernetFrame_IsDecapsulatedAndDelivered()
    {
        var arp = ArpPacket.CreateRequest(SenderMac, SenderIp, TargetIp);
        var frame = EthernetFrame.Create(SenderMac, MacAddress.Broadcast, EtherType.Arp, arp);

        var result = new ArpProcessor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
    }

    [Fact]
    public void Process_EthernetFrameWithNonArpEtherType_IsInvalid()
    {
        var frame = EthernetFrame.Create(SenderMac, DstMac, EtherType.IPv4, RawPayload.OfSize(8));

        var result = new ArpProcessor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
        Assert.Contains("does not carry ARP", result.Detail);
    }

    [Fact]
    public void Process_FrameClaimingArpButCarryingSomethingElse_IsInvalid()
    {
        var frame = EthernetFrame.Create(SenderMac, MacAddress.Broadcast, EtherType.Arp, RawPayload.FromText("not arp"));

        var result = new ArpProcessor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
        Assert.Contains("not an ARP message", result.Detail);
    }

    [Fact]
    public void Process_NonArpPayload_IsInvalid()
    {
        var result = new ArpProcessor().Process(PacketCarrying(RawPayload.FromText("plain")));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_BareArpReply_IsDelivered()
    {
        var reply = ArpPacket.CreateReply(
            DstMac, TargetIp, SenderMac, SenderIp);

        var result = new ArpProcessor().Process(PacketCarrying(reply));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Contains("is at", result.Detail);
    }

    [Fact]
    public void RunThroughThePacketEngine_ValidArp_EndsDelivered()
    {
        var engine = new PacketEngine();
        var arp = ArpPacket.CreateRequest(SenderMac, SenderIp, TargetIp);
        var packet = engine.CreatePacket(new Pc("A"), new Pc("B"), "ARP", arp);

        var result = engine.Process(packet, new ArpProcessor());

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Equal(PacketState.Delivered, packet.State);
    }

    [Fact]
    public void Process_NullPacket_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ArpProcessor().Process(null!));
    }
}
