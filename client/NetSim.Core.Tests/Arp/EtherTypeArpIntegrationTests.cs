using NetSim.Core.Arp;
using NetSim.Core.Devices;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.Arp;

/// <summary>
/// Phase 20 (brief section 11): an ARP message rides inside an <see cref="EthernetFrame"/> keyed
/// by <see cref="EtherType.Arp"/> (0x0806) - the same encapsulation mechanism IPv4 and IPv6 use,
/// no separate protocol-identification system.
/// </summary>
public class EtherTypeArpIntegrationTests
{
    private static readonly MacAddress SenderMac = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly IPv4Address SenderIp = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address TargetIp = IPv4Address.Parse("192.168.1.20");

    [Fact]
    public void ArpRequest_LivesInsideAnEthernetBroadcastFrame_KeyedByEtherType0x0806()
    {
        var arp = ArpPacket.CreateRequest(SenderMac, SenderIp, TargetIp);
        var frame = EthernetFrame.Create(SenderMac, MacAddress.Broadcast, EtherType.Arp, arp);

        Assert.Equal(0x0806, frame.EtherType.Value);
        Assert.Equal("ARP", frame.EtherType.Name);
        Assert.True(frame.EtherType == EtherType.Arp);
        Assert.Same(arp, frame.Payload);
        Assert.Same(arp, frame.EncapsulatedPayload);
        Assert.True(frame.IsBroadcast);

        Assert.True(frame.Validate().IsValid);
        Assert.Equal(EthernetFrame.HeaderSizeBytes + arp.Length, frame.Length);
    }

    [Fact]
    public void ArpReply_LivesInsideAUnicastEthernetFrame()
    {
        var replierMac = MacAddress.Parse("AA:BB:CC:DD:EE:FF");
        var arp = ArpPacket.CreateReply(replierMac, TargetIp, SenderMac, SenderIp);
        var frame = EthernetFrame.Create(replierMac, SenderMac, EtherType.Arp, arp);

        Assert.Equal(EtherType.Arp, frame.EtherType);
        Assert.True(frame.IsUnicast);
        Assert.Equal(SenderMac, frame.DestinationMac);
        Assert.True(frame.Validate().IsValid);
    }

    [Fact]
    public void ArpFrame_RidesInsideAGenericPacket_WithNothingArpSpecificInTheEngine()
    {
        var arp = ArpPacket.CreateRequest(SenderMac, SenderIp, TargetIp);
        var frame = EthernetFrame.Create(SenderMac, MacAddress.Broadcast, EtherType.Arp, arp);
        var packet = new Packet(new Pc("A"), new Pc("B"), "Ethernet", frame);

        Assert.Same(frame, packet.Payload);
        var carriedArp = Assert.IsType<ArpPacket>(((EthernetFrame)packet.Payload!).Payload);
        Assert.Equal(TargetIp, carriedArp.TargetProtocolAddress);
    }
}
