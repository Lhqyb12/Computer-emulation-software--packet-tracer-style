using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class InterfaceSpeedTests
{
    [Fact]
    public void NamedSpeeds_HaveExpectedBitsPerSecond()
    {
        Assert.Equal(10_000_000L, InterfaceSpeed.Mbps10.BitsPerSecond);
        Assert.Equal(100_000_000L, InterfaceSpeed.Mbps100.BitsPerSecond);
        Assert.Equal(1_000_000_000L, InterfaceSpeed.Gbps1.BitsPerSecond);
        Assert.Equal(10_000_000_000L, InterfaceSpeed.Gbps10.BitsPerSecond);
    }

    [Theory]
    [InlineData(10_000_000L, "10 Mbps")]
    [InlineData(100_000_000L, "100 Mbps")]
    [InlineData(1_000_000_000L, "1 Gbps")]
    [InlineData(10_000_000_000L, "10 Gbps")]
    [InlineData(1_544_000L, "1.544 Mbps")]
    [InlineData(9_600L, "9.6 Kbps")]
    public void ToDisplayString_FormatsHumanReadably(long bps, string expected)
    {
        Assert.Equal(expected, new InterfaceSpeed(bps).ToDisplayString());
    }

    [Fact]
    public void IsComparable_ByBitsPerSecond()
    {
        Assert.True(InterfaceSpeed.Mbps10 < InterfaceSpeed.Mbps100);
        Assert.True(InterfaceSpeed.Gbps1 > InterfaceSpeed.Mbps100);
        Assert.Equal(InterfaceSpeed.Gbps1, InterfaceSpeed.FromMegabitsPerSecond(1000));
    }

    [Fact]
    public void EqualityAndHashCode_TrackBitsPerSecond()
    {
        Assert.Equal(new InterfaceSpeed(1000), new InterfaceSpeed(1000));
        Assert.NotEqual(new InterfaceSpeed(1000), new InterfaceSpeed(2000));
        Assert.Equal(new InterfaceSpeed(1000).GetHashCode(), new InterfaceSpeed(1000).GetHashCode());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveSpeed(long bps)
    {
        Assert.Throws<DomainException>(() => new InterfaceSpeed(bps));
    }
}
