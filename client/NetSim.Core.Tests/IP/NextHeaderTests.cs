using NetSim.Core.IP;

namespace NetSim.Core.Tests.IP;

public class NextHeaderTests
{
    [Fact]
    public void WellKnownValues_HaveTheExpectedNumbersAndNames()
    {
        Assert.Equal(0, NextHeader.HopByHopOptions.Value);
        Assert.Equal(6, NextHeader.Tcp.Value);
        Assert.Equal(17, NextHeader.Udp.Value);
        Assert.Equal(43, NextHeader.Routing.Value);
        Assert.Equal(44, NextHeader.Fragment.Value);
        Assert.Equal(58, NextHeader.IcmpV6.Value);
        Assert.Equal(59, NextHeader.None.Value);
        Assert.Equal(60, NextHeader.DestinationOptions.Value);

        Assert.Equal("TCP", NextHeader.Tcp.Name);
        Assert.Equal("ICMPv6", NextHeader.IcmpV6.Name);
        Assert.Equal("Fragment", NextHeader.Fragment.Name);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(43, true)]
    [InlineData(44, true)]
    [InlineData(60, true)]
    [InlineData(6, false)]
    [InlineData(17, false)]
    [InlineData(58, false)]
    [InlineData(59, false)]
    public void IsExtensionHeader_IdentifiesTheHeaderChainMembers(byte value, bool isExtension)
    {
        Assert.Equal(isExtension, new NextHeader(value).IsExtensionHeader);
    }

    [Fact]
    public void UpperLayerVsExtensionVsNone_AreDistinct()
    {
        Assert.True(NextHeader.Tcp.IsUpperLayer);
        Assert.False(NextHeader.Fragment.IsUpperLayer);
        Assert.False(NextHeader.None.IsUpperLayer);
        Assert.False(NextHeader.None.IsExtensionHeader);
    }

    [Fact]
    public void UnknownValue_IsRepresentableWithoutThrowing()
    {
        var value = new NextHeader(200);

        Assert.False(value.IsKnown);
        Assert.Equal("Unknown", value.Name);
        Assert.Contains("200", value.ToString());
    }

    [Fact]
    public void Default_IsHopByHopOptionsZero_AndEqualityIsValueBased()
    {
        Assert.Equal(NextHeader.HopByHopOptions, default(NextHeader));
        Assert.Equal(new NextHeader(6), NextHeader.Tcp);
        Assert.True(new NextHeader(58) == NextHeader.IcmpV6);
        Assert.NotEqual(NextHeader.Tcp, NextHeader.Udp);
    }
}
