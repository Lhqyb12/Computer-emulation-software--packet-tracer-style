using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;
using NetSim.Core.Tests.Packets;

namespace NetSim.Core.Tests.IP;

public class IPv4PacketTests
{
    private static readonly IPv4Address Src = IPv4Address.Parse("192.168.1.10");
    private static readonly IPv4Address Dst = IPv4Address.Parse("192.168.1.20");

    [Fact]
    public void Create_SetsHeaderAndPayload_WithSensibleDefaults()
    {
        var payload = RawPayload.FromText("hello");

        var packet = IPv4Packet.Create(Src, Dst, payload);

        Assert.Equal(Src, packet.SourceAddress);
        Assert.Equal(Dst, packet.DestinationAddress);
        Assert.Equal(IPv4Packet.DefaultTimeToLive, packet.TimeToLive);
        Assert.Equal(ProtocolNumber.Unspecified, packet.Protocol);
        Assert.Equal(0, packet.Identification);
        Assert.Equal(IPv4Flags.None, packet.Flags);
        Assert.Equal(0, packet.FragmentOffset);
        Assert.Same(payload, packet.Payload);
        Assert.Same(payload, packet.EncapsulatedPayload);
    }

    [Fact]
    public void Create_WithNoPayload_UsesEmptyLeaf_AndMinimumTotalLength()
    {
        var packet = IPv4Packet.Create(Src, Dst);

        Assert.Same(RawPayload.Empty, packet.Payload);
        Assert.Equal(IPv4Header.MinimumHeaderLengthBytes, packet.TotalLength);
        Assert.Equal(IPv4Header.MinimumHeaderLengthBytes, packet.Length);
    }

    [Fact]
    public void IsAPacketPayload_WithIPv4TypeAndAggregatedLength()
    {
        var packet = IPv4Packet.Create(Src, Dst, RawPayload.OfSize(200), ProtocolNumber.Tcp);

        Assert.IsAssignableFrom<IPacketPayload>(packet);
        Assert.Equal("IPv4", packet.PayloadType);
        Assert.Equal(IPv4Header.MinimumHeaderLengthBytes + 200, packet.Length);
        Assert.Equal(IPv4Header.MinimumHeaderLengthBytes + 200, packet.TotalLength);
    }

    [Fact]
    public void Create_CarriesTheProtocolIdentificationFlagsAndFragmentOffset()
    {
        var packet = IPv4Packet.Create(
            Src, Dst, RawPayload.OfSize(10), ProtocolNumber.Udp,
            timeToLive: 32, identification: 7777, flags: IPv4Flags.DontFragment, fragmentOffset: 64);

        Assert.Equal(ProtocolNumber.Udp, packet.Protocol);
        Assert.Equal(32, packet.TimeToLive);
        Assert.Equal(7777, packet.Identification);
        Assert.Equal(IPv4Flags.DontFragment, packet.Flags);
        Assert.Equal(64, packet.FragmentOffset);
    }

    [Fact]
    public void Create_RejectsAnUnsetDestination()
    {
        Assert.Throws<DomainException>(() => IPv4Packet.Create(Src, IPv4Address.Any));
    }

    [Theory]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    public void Create_RejectsAnInvalidSourceAddress(string source)
    {
        Assert.Throws<DomainException>(() => IPv4Packet.Create(IPv4Address.Parse(source), Dst));
    }

    [Fact]
    public void Create_RejectsATtlOfZero()
    {
        Assert.Throws<DomainException>(() => IPv4Packet.Create(Src, Dst, timeToLive: 0));
    }

    [Fact]
    public void Validate_IsValid_ForAWellFormedPacket()
    {
        Assert.True(IPv4Packet.Create(Src, Dst, RawPayload.OfSize(64), ProtocolNumber.Tcp).Validate().IsValid);
    }

    [Fact]
    public void Validate_SurfacesABrokenEncapsulatedPayload()
    {
        var badInner = new StubPayload { ValidationResult = PacketValidationResult.Invalid("truncated segment") };
        var packet = IPv4Packet.Create(Src, Dst, badInner);

        var result = packet.Validate();

        Assert.False(result.IsValid);
        Assert.Contains("truncated segment", result.Errors);
    }

    [Fact]
    public void WithDecrementedTimeToLive_ReturnsACopyWithTtlMinusOne_AndLeavesTheOriginal()
    {
        var original = IPv4Packet.Create(Src, Dst, RawPayload.OfSize(16), ProtocolNumber.Tcp, timeToLive: 5, identification: 99);

        var forwarded = original.WithDecrementedTimeToLive();

        Assert.Equal(5, original.TimeToLive);
        Assert.Equal(4, forwarded.TimeToLive);
        Assert.Equal(original.SourceAddress, forwarded.SourceAddress);
        Assert.Equal(original.DestinationAddress, forwarded.DestinationAddress);
        Assert.Equal(original.Identification, forwarded.Identification);
        Assert.Equal(original.TotalLength, forwarded.TotalLength);
        Assert.Same(original.Payload, forwarded.Payload);
    }

    [Fact]
    public void WithDecrementedTimeToLive_ThrowsWhenAlreadyZero()
    {
        var packet = IPv4Packet.Create(Src, Dst, timeToLive: 1);
        var expired = packet.WithDecrementedTimeToLive();

        Assert.True(expired.IsTimeToLiveExhausted);
        Assert.Throws<DomainException>(() => expired.WithDecrementedTimeToLive());
    }

    [Fact]
    public void Packet_RidesInsideAGenericPacket()
    {
        var ipPacket = IPv4Packet.Create(Src, Dst, RawPayload.FromText("payload"), ProtocolNumber.Udp);
        var packet = new Packet(new Pc("A"), new Pc("B"), "IPv4", ipPacket);

        Assert.Same(ipPacket, packet.Payload);
        Assert.Equal("IPv4", packet.Payload!.PayloadType);
        Assert.True(packet.Validate().IsValid);
    }
}
