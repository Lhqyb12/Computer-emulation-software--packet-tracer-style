using NetSim.Core.Common.Exceptions;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.IP;

public class IPv6HeaderTests
{
    private static readonly IPv6Address Src = IPv6Address.Parse("2001:db8::1");
    private static readonly IPv6Address Dst = IPv6Address.Parse("2001:db8::2");

    private static IPv6Header HeaderOf(IPv6Packet packet) => packet.Header;

    [Fact]
    public void VersionIsAlwaysSix_AndTheFixedHeaderIsFortyBytes()
    {
        Assert.Equal(6, IPv6Header.Version);
        Assert.Equal(40, IPv6Header.HeaderLengthBytes);
    }

    [Fact]
    public void CarriesEveryHeaderField()
    {
        var packet = IPv6Packet.Create(
            Src, Dst, RawPayload.OfSize(100), NextHeader.Udp,
            hopLimit: 200, trafficClass: 0x28, flowLabel: 0xABCDE);
        var header = HeaderOf(packet);

        Assert.Equal(Src, header.SourceAddress);
        Assert.Equal(Dst, header.DestinationAddress);
        Assert.Equal(NextHeader.Udp, header.NextHeader);
        Assert.Equal(200, header.HopLimit);
        Assert.Equal(0x28, header.TrafficClass);
        Assert.Equal(0xABCDE, header.FlowLabel);
        Assert.Equal(100, header.PayloadLengthBytes);
    }

    [Fact]
    public void PayloadLength_IsTheLengthAfterTheFixedHeader_NotTheWholePacket()
    {
        var packet = IPv6Packet.Create(Src, Dst, RawPayload.OfSize(1000), NextHeader.Tcp);

        Assert.Equal(1000, packet.Header.PayloadLengthBytes);
        Assert.Equal(1040, packet.Length);
    }

    [Fact]
    public void Create_RejectsAFlowLabelOutOfRange()
    {
        Assert.Throws<DomainException>(() =>
            IPv6Packet.Create(Src, Dst, flowLabel: 0x100000));
        Assert.Throws<DomainException>(() =>
            IPv6Packet.Create(Src, Dst, flowLabel: -1));
    }

    [Fact]
    public void ToBytes_LaysOutTheFortyByteNetworkOrderHeader()
    {
        var packet = IPv6Packet.Create(
            Src, Dst, RawPayload.OfSize(0x1234), NextHeader.Tcp,
            hopLimit: 64, trafficClass: 0x00, flowLabel: 0x00000);
        var bytes = packet.Header.ToBytes();

        Assert.Equal(40, bytes.Length);
        Assert.Equal(0x60, bytes[0]); // version 6 in the high nibble, traffic class 0
        Assert.Equal(0x00, bytes[1]);
        Assert.Equal(0x12, bytes[4]); // payload length high byte
        Assert.Equal(0x34, bytes[5]); // payload length low byte
        Assert.Equal(6, bytes[6]);    // next header = TCP
        Assert.Equal(64, bytes[7]);   // hop limit
        Assert.Equal(Src.GetBytes(), bytes[8..24]);
        Assert.Equal(Dst.GetBytes(), bytes[24..40]);
    }

    [Fact]
    public void ToBytes_PacksVersionTrafficClassAndFlowLabelTogether()
    {
        var packet = IPv6Packet.Create(Src, Dst, trafficClass: 0xFF, flowLabel: 0xFFFFF);
        var bytes = packet.Header.ToBytes();

        // 0110 1111 1111 1111 1111 1111 1111 1111
        Assert.Equal(0x6F, bytes[0]);
        Assert.Equal(0xFF, bytes[1]);
        Assert.Equal(0xFF, bytes[2]);
        Assert.Equal(0xFF, bytes[3]);
    }

    [Fact]
    public void Validate_IsValid_ForAWellFormedHeader()
    {
        Assert.True(IPv6Packet.Create(Src, Dst, RawPayload.OfSize(10), NextHeader.Tcp).Header.Validate().IsValid);
    }
}
