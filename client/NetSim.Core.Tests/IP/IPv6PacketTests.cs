using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tests.Packets;

namespace NetSim.Core.Tests.IP;

public class IPv6PacketTests
{
    private static readonly IPv6Address Src = IPv6Address.Parse("2001:db8:1::10");
    private static readonly IPv6Address Dst = IPv6Address.Parse("2001:db8:1::20");

    [Fact]
    public void Create_SetsHeaderAndPayload_WithSensibleDefaults()
    {
        var payload = RawPayload.FromText("hello");

        var packet = IPv6Packet.Create(Src, Dst, payload);

        Assert.Equal(Src, packet.SourceAddress);
        Assert.Equal(Dst, packet.DestinationAddress);
        Assert.Equal(IPv6Packet.DefaultHopLimit, packet.HopLimit);
        Assert.Equal(default(NextHeader), packet.NextHeader);
        Assert.Equal(0, packet.TrafficClass);
        Assert.Equal(0, packet.FlowLabel);
        Assert.Empty(packet.ExtensionHeaders);
        Assert.Same(payload, packet.Payload);
        Assert.Same(payload, packet.EncapsulatedPayload);
    }

    [Fact]
    public void Create_WithNoPayload_UsesEmptyLeaf_AndMinimumLength()
    {
        var packet = IPv6Packet.Create(Src, Dst);

        Assert.Same(RawPayload.Empty, packet.Payload);
        Assert.Equal(0, packet.PayloadLength);
        Assert.Equal(IPv6Header.HeaderLengthBytes, packet.Length);
    }

    [Fact]
    public void IsAPacketPayload_WithIPv6TypeAndAggregatedLength()
    {
        var packet = IPv6Packet.Create(Src, Dst, RawPayload.OfSize(200), NextHeader.Tcp);

        Assert.IsAssignableFrom<IPacketPayload>(packet);
        Assert.Equal("IPv6", packet.PayloadType);
        Assert.Equal(IPv6Header.HeaderLengthBytes + 200, packet.Length);
        Assert.Equal(200, packet.PayloadLength);
    }

    [Fact]
    public void Create_CarriesTheNextHeaderTrafficClassFlowLabelAndHopLimit()
    {
        var packet = IPv6Packet.Create(
            Src, Dst, RawPayload.OfSize(10), NextHeader.Udp,
            hopLimit: 32, trafficClass: 16, flowLabel: 12345);

        Assert.Equal(NextHeader.Udp, packet.NextHeader);
        Assert.Equal(32, packet.HopLimit);
        Assert.Equal(16, packet.TrafficClass);
        Assert.Equal(12345, packet.FlowLabel);
    }

    [Fact]
    public void Create_RejectsAnUnsetDestination()
    {
        Assert.Throws<DomainException>(() => IPv6Packet.Create(Src, IPv6Address.Unspecified));
    }

    [Fact]
    public void Create_RejectsAMulticastSource()
    {
        Assert.Throws<DomainException>(() => IPv6Packet.Create(IPv6Address.Parse("ff02::1"), Dst));
    }

    [Fact]
    public void Create_AllowsAnUnspecifiedSource()
    {
        var packet = IPv6Packet.Create(IPv6Address.Unspecified, Dst);

        Assert.Equal(IPv6Address.Unspecified, packet.SourceAddress);
    }

    [Fact]
    public void Create_RejectsAHopLimitOfZero()
    {
        Assert.Throws<DomainException>(() => IPv6Packet.Create(Src, Dst, hopLimit: 0));
    }

    [Fact]
    public void Validate_IsValid_ForAWellFormedPacket()
    {
        Assert.True(IPv6Packet.Create(Src, Dst, RawPayload.OfSize(64), NextHeader.Tcp).Validate().IsValid);
    }

    [Fact]
    public void Validate_SurfacesABrokenEncapsulatedPayload()
    {
        var badInner = new StubPayload { ValidationResult = PacketValidationResult.Invalid("truncated segment") };
        var packet = IPv6Packet.Create(Src, Dst, badInner);

        var result = packet.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("truncated segment", result.Errors);
    }

    [Fact]
    public void WithDecrementedHopLimit_ReturnsACopyWithHopLimitMinusOne_AndLeavesTheOriginal()
    {
        var original = IPv6Packet.Create(Src, Dst, RawPayload.OfSize(16), NextHeader.Tcp, hopLimit: 5, flowLabel: 99);

        var forwarded = original.WithDecrementedHopLimit();

        Assert.Equal(5, original.HopLimit);
        Assert.Equal(4, forwarded.HopLimit);
        Assert.Equal(original.SourceAddress, forwarded.SourceAddress);
        Assert.Equal(original.DestinationAddress, forwarded.DestinationAddress);
        Assert.Equal(original.FlowLabel, forwarded.FlowLabel);
        Assert.Equal(original.PayloadLength, forwarded.PayloadLength);
        Assert.Same(original.Payload, forwarded.Payload);
    }

    [Fact]
    public void WithDecrementedHopLimit_ThrowsWhenAlreadyZero()
    {
        var packet = IPv6Packet.Create(Src, Dst, hopLimit: 1);
        var expired = packet.WithDecrementedHopLimit();

        Assert.True(expired.IsHopLimitExhausted);
        Assert.Throws<DomainException>(() => expired.WithDecrementedHopLimit());
    }

    [Fact]
    public void Packet_RidesInsideAGenericPacket()
    {
        var ipPacket = IPv6Packet.Create(Src, Dst, RawPayload.FromText("payload"), NextHeader.Udp);
        var packet = new Packet(new Pc("A"), new Pc("B"), "IPv6", ipPacket);

        Assert.Same(ipPacket, packet.Payload);
        Assert.Equal("IPv6", packet.Payload!.PayloadType);
        Assert.True(packet.Validate().IsValid);
    }

    [Fact]
    public void ExtensionHeaders_AreIncludedInThePayloadLength_AndValidated()
    {
        var extension = new TestExtensionHeader(NextHeader.DestinationOptions, NextHeader.Tcp, lengthBytes: 8);
        var packet = IPv6Packet.Create(
            Src, Dst, RawPayload.OfSize(20), NextHeader.DestinationOptions, extensionHeaders: [extension]);

        Assert.Single(packet.ExtensionHeaders);
        Assert.Equal(28, packet.PayloadLength); // 8 (extension) + 20 (payload)
        Assert.Equal(IPv6Header.HeaderLengthBytes + 28, packet.Length);
        Assert.True(packet.Validate().IsValid);
    }
}
