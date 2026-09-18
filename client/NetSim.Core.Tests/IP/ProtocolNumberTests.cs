using NetSim.Core.IP;

namespace NetSim.Core.Tests.IP;

public class ProtocolNumberTests
{
    [Fact]
    public void WellKnownValues_HaveTheExpectedNumbersAndNames()
    {
        Assert.Equal(1, ProtocolNumber.Icmp.Value);
        Assert.Equal(6, ProtocolNumber.Tcp.Value);
        Assert.Equal(17, ProtocolNumber.Udp.Value);

        Assert.Equal("ICMP", ProtocolNumber.Icmp.Name);
        Assert.Equal("TCP", ProtocolNumber.Tcp.Name);
        Assert.Equal("UDP", ProtocolNumber.Udp.Name);

        Assert.True(ProtocolNumber.Tcp.IsKnown);
        Assert.True(ProtocolNumber.Tcp.IsSpecified);
    }

    [Fact]
    public void Unspecified_IsNotSpecifiedAndNotKnown()
    {
        Assert.False(ProtocolNumber.Unspecified.IsSpecified);
        Assert.False(ProtocolNumber.Unspecified.IsKnown);
        Assert.Equal("Unspecified", ProtocolNumber.Unspecified.Name);
        Assert.Equal(ProtocolNumber.Unspecified, default(ProtocolNumber));
    }

    [Fact]
    public void UnknownValue_IsRepresentableWithoutThrowing()
    {
        var gre = new ProtocolNumber(47);

        Assert.True(gre.IsSpecified);
        Assert.False(gre.IsKnown);
        Assert.Equal("Unknown", gre.Name);
        Assert.Contains("47", gre.ToString());
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        Assert.Equal(new ProtocolNumber(6), ProtocolNumber.Tcp);
        Assert.True(new ProtocolNumber(6) == ProtocolNumber.Tcp);
        Assert.NotEqual(ProtocolNumber.Tcp, ProtocolNumber.Udp);
    }
}
