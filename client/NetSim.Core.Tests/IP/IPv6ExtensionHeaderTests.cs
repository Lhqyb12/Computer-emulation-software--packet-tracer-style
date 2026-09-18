using NetSim.Core.IP;
using NetSim.Core.Networking;
using NetSim.Core.Packets;

namespace NetSim.Core.Tests.IP;

/// <summary>
/// A minimal concrete <see cref="IPv6ExtensionHeader"/> used only to prove the abstraction can be
/// extended and chained. No real extension header (hop-by-hop options, routing, fragment,
/// destination options) is implemented in Phase 19.
/// </summary>
internal sealed class TestExtensionHeader : IPv6ExtensionHeader
{
    public TestExtensionHeader(NextHeader headerType, NextHeader nextHeader, int lengthBytes)
        : base(headerType, nextHeader, lengthBytes)
    {
    }
}

public class IPv6ExtensionHeaderTests
{
    [Fact]
    public void AConcreteHeader_DeclaresItsTypeTheNextHeaderAndItsLength()
    {
        var header = new TestExtensionHeader(NextHeader.Routing, NextHeader.Tcp, lengthBytes: 24);

        Assert.Equal(NextHeader.Routing, header.HeaderType);
        Assert.Equal(NextHeader.Tcp, header.NextHeader);
        Assert.Equal(24, header.LengthBytes);
        Assert.True(header.Validate().IsValid);
    }

    [Fact]
    public void BaseValidation_RejectsANegativeLength()
    {
        var header = new TestExtensionHeader(NextHeader.HopByHopOptions, NextHeader.Udp, lengthBytes: -1);

        Assert.False(header.Validate().IsValid);
    }

    [Fact]
    public void ExtensionHeaders_CanBeChained_BetweenTheFixedHeaderAndThePayload()
    {
        // IPv6 Header -> Hop-by-Hop Options -> Destination Options -> UDP
        var hopByHop = new TestExtensionHeader(NextHeader.HopByHopOptions, NextHeader.DestinationOptions, lengthBytes: 8);
        var destinationOptions = new TestExtensionHeader(NextHeader.DestinationOptions, NextHeader.Udp, lengthBytes: 8);

        var packet = IPv6Packet.Create(
            IPv6Address.Parse("2001:db8::1"),
            IPv6Address.Parse("2001:db8::2"),
            RawPayload.OfSize(32),
            NextHeader.HopByHopOptions,
            extensionHeaders: [hopByHop, destinationOptions]);

        Assert.Equal(2, packet.ExtensionHeaders.Count);
        Assert.Equal(NextHeader.HopByHopOptions, packet.NextHeader);
        Assert.Equal(NextHeader.Udp, packet.ExtensionHeaders[^1].NextHeader);
        Assert.Equal(48, packet.PayloadLength); // 8 + 8 + 32
        Assert.True(packet.Validate().IsValid);
    }
}
