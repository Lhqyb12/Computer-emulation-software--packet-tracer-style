using NetSim.Core.Arp;
using NetSim.Core.Common.Exceptions;
using NetSim.Core.Ethernet;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Arp;

public class ArpPacketTests
{
    private static readonly MacAddress SenderMac = MacAddress.Parse("00:11:22:33:44:55");
    private static readonly IPv4Address SenderIp = IPv4Address.Parse("192.168.1.10");
    private static readonly MacAddress TargetMac = MacAddress.Parse("AA:BB:CC:DD:EE:FF");
    private static readonly IPv4Address TargetIp = IPv4Address.Parse("192.168.1.20");

    [Fact]
    public void CreateRequest_FillsTheStandardEthernetIPv4Fields()
    {
        var request = ArpPacket.CreateRequest(SenderMac, SenderIp, TargetIp);

        Assert.Equal(ArpOperation.Request, request.Operation);
        Assert.True(request.IsRequest);
        Assert.False(request.IsReply);

        Assert.Equal(ArpHardwareType.Ethernet, request.HardwareType);
        Assert.Equal(1, request.HardwareType.Value);
        Assert.Equal(EtherType.IPv4, request.ProtocolType);
        Assert.Equal(0x0800, request.ProtocolType.Value);
        Assert.Equal(6, request.HardwareAddressLength);
        Assert.Equal(4, request.ProtocolAddressLength);

        Assert.Equal(SenderMac, request.SenderHardwareAddress);
        Assert.Equal(SenderIp, request.SenderProtocolAddress);
        Assert.Equal(TargetIp, request.TargetProtocolAddress);

        // A normal request does not know the target MAC yet.
        Assert.Equal(MacAddress.Zero, request.TargetHardwareAddress);
        Assert.True(request.TargetHardwareAddress.IsUnspecified);
    }

    [Fact]
    public void CreateReply_CarriesBothPairsAndIsAddressedToTheRequester()
    {
        var reply = ArpPacket.CreateReply(TargetMac, TargetIp, SenderMac, SenderIp);

        Assert.Equal(ArpOperation.Reply, reply.Operation);
        Assert.True(reply.IsReply);
        Assert.Equal(TargetMac, reply.SenderHardwareAddress);
        Assert.Equal(TargetIp, reply.SenderProtocolAddress);
        Assert.Equal(SenderMac, reply.TargetHardwareAddress);
        Assert.Equal(SenderIp, reply.TargetProtocolAddress);
    }

    [Fact]
    public void IPacketPayload_Shape_Is28BytesAndCarriesNothing()
    {
        var request = ArpPacket.CreateRequest(SenderMac, SenderIp, TargetIp);

        Assert.Equal("ARP", request.PayloadType);
        Assert.Equal(28, request.Length);
        Assert.Equal(ArpPacket.EthernetIPv4LengthBytes, request.Length);
        Assert.Null(request.EncapsulatedPayload);
    }

    [Fact]
    public void Request_And_Reply_Validate()
    {
        Assert.True(ArpPacket.CreateRequest(SenderMac, SenderIp, TargetIp).Validate().IsValid);
        Assert.True(ArpPacket.CreateReply(TargetMac, TargetIp, SenderMac, SenderIp).Validate().IsValid);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    public void CreateRequest_RejectsANonHostSenderIPv4(string bad)
    {
        Assert.Throws<DomainException>(() => ArpPacket.CreateRequest(SenderMac, IPv4Address.Parse(bad), TargetIp));
    }

    [Fact]
    public void CreateRequest_RejectsANonUnicastSenderMac()
    {
        Assert.Throws<DomainException>(() => ArpPacket.CreateRequest(MacAddress.Broadcast, SenderIp, TargetIp));
        Assert.Throws<DomainException>(() => ArpPacket.CreateRequest(MacAddress.Zero, SenderIp, TargetIp));
    }

    [Fact]
    public void CreateRequest_RejectsAnUnsetTargetIPv4()
    {
        Assert.Throws<DomainException>(() => ArpPacket.CreateRequest(SenderMac, SenderIp, IPv4Address.Any));
    }

    [Fact]
    public void CreateReply_RejectsANonUnicastTargetMac_OrUnsetTargetIPv4()
    {
        Assert.Throws<DomainException>(() => ArpPacket.CreateReply(TargetMac, TargetIp, MacAddress.Broadcast, SenderIp));
        Assert.Throws<DomainException>(() => ArpPacket.CreateReply(TargetMac, TargetIp, MacAddress.Zero, SenderIp));
        Assert.Throws<DomainException>(() => ArpPacket.CreateReply(TargetMac, TargetIp, SenderMac, IPv4Address.Any));
    }

    [Fact]
    public void ToString_ReadsLikeArp()
    {
        Assert.Equal(
            "ARP Request who-has 192.168.1.20 tell 192.168.1.10 (00:11:22:33:44:55)",
            ArpPacket.CreateRequest(SenderMac, SenderIp, TargetIp).ToString());
        Assert.Equal(
            "ARP Reply 192.168.1.20 is-at AA:BB:CC:DD:EE:FF",
            ArpPacket.CreateReply(TargetMac, TargetIp, SenderMac, SenderIp).ToString());
    }
}
