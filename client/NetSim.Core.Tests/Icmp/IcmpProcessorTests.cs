using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Icmp;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Icmp;

public class IcmpProcessorTests
{
    private static readonly MacAddress SrcMac = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly MacAddress DstMac = MacAddress.Parse("AA:BB:CC:DD:EE:FF");
    private static readonly IPv4Address SourceIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address DestinationIp = IPv4Address.Parse("192.168.1.20");

    private static Packet PacketCarrying(IPacketPayload? payload) => new(new Pc("A"), new Pc("B"), "ICMP", payload);

    private static IPv4Packet IcmpInIPv4(IcmpMessage icmp) => IPv4Packet.Create(SourceIp, DestinationIp, icmp, ProtocolNumber.Icmp);

    [Fact]
    public void Process_BareEchoRequest_IsDelivered()
    {
        var icmp = IcmpMessage.CreateEchoRequest(1, 1);

        var result = new IcmpProcessor().Process(PacketCarrying(icmp));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
    }

    [Fact]
    public void Process_EchoRequestInsideIPv4_IsDecapsulatedAndDelivered()
    {
        var icmp = IcmpMessage.CreateEchoRequest(1, 1);
        var ipPacket = IcmpInIPv4(icmp);

        var result = new IcmpProcessor().Process(PacketCarrying(ipPacket));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
    }

    [Fact]
    public void Process_EchoRequestInsideIPv4InsideEthernet_IsDecapsulatedAndDelivered()
    {
        var icmp = IcmpMessage.CreateEchoRequest(1, 1);
        var ipPacket = IcmpInIPv4(icmp);
        var frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.IPv4, ipPacket);

        var result = new IcmpProcessor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
    }

    [Fact]
    public void Process_IPv4PacketWithNonIcmpProtocol_IsInvalid()
    {
        var ipPacket = IPv4Packet.Create(SourceIp, DestinationIp, RawPayload.OfSize(4), ProtocolNumber.Tcp);

        var result = new IcmpProcessor().Process(PacketCarrying(ipPacket));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
        Assert.Contains("does not carry ICMP", result.Detail);
    }

    [Fact]
    public void Process_IPv4PacketClaimingIcmpButCarryingSomethingElse_IsInvalid()
    {
        var ipPacket = IPv4Packet.Create(SourceIp, DestinationIp, RawPayload.OfSize(4), ProtocolNumber.Icmp);

        var result = new IcmpProcessor().Process(PacketCarrying(ipPacket));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
        Assert.Contains("not an ICMP message", result.Detail);
    }

    [Fact]
    public void Process_EthernetFrameWithNonIPv4EtherType_IsInvalid()
    {
        var frame = EthernetFrame.Create(SrcMac, DstMac, EtherType.Arp, RawPayload.OfSize(4));

        var result = new IcmpProcessor().Process(PacketCarrying(frame));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_NonIcmpPayload_IsInvalid()
    {
        var result = new IcmpProcessor().Process(PacketCarrying(RawPayload.FromText("plain")));

        Assert.Equal(PacketProcessingOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public void Process_InvalidChecksum_IsDropped()
    {
        var icmp = IcmpMessage.CreateEchoRequest(1, 1, RawPayload.FromText("Hello"));
        var corrupted = icmp.WithChecksum(unchecked((ushort)(icmp.Checksum + 1)));

        var result = new IcmpProcessor().Process(PacketCarrying(corrupted));

        Assert.Equal(PacketProcessingOutcome.Dropped, result.Outcome);
        Assert.Same(IcmpDropReasons.InvalidChecksum, result.DropReason);
    }

    [Fact]
    public void Process_BareDestinationUnreachable_IsDelivered()
    {
        var original = IcmpOriginalDatagramInfo.FromPacket(
            IPv4Packet.Create(SourceIp, DestinationIp, RawPayload.OfSize(4), ProtocolNumber.Icmp, timeToLive: 1));
        var icmp = IcmpMessage.CreateDestinationUnreachable(original, IcmpCode.HostUnreachable);

        var result = new IcmpProcessor().Process(PacketCarrying(icmp));

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
    }

    [Fact]
    public void RunThroughThePacketEngine_ValidEcho_EndsDelivered()
    {
        var engine = new PacketEngine();
        var icmp = IcmpMessage.CreateEchoRequest(1, 1);
        var packet = engine.CreatePacket(new Pc("A"), new Pc("B"), "ICMP", icmp);

        var result = engine.Process(packet, new IcmpProcessor());

        Assert.Equal(PacketProcessingOutcome.Delivered, result.Outcome);
        Assert.Equal(PacketState.Delivered, packet.State);
    }

    [Fact]
    public void Process_NullPacket_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IcmpProcessor().Process(null!));
    }
}
