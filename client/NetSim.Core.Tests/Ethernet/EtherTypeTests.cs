using NetSim.Core.Ethernet;

namespace NetSim.Core.Tests.Ethernet;

public class EtherTypeTests
{
    [Fact]
    public void WellKnownValues_HaveTheExpectedNumbersAndNames()
    {
        Assert.Equal(0x0800, EtherType.IPv4.Value);
        Assert.Equal(0x0806, EtherType.Arp.Value);
        Assert.Equal(0x86DD, EtherType.IPv6.Value);

        Assert.Equal("IPv4", EtherType.IPv4.Name);
        Assert.Equal("ARP", EtherType.Arp.Name);
        Assert.Equal("IPv6", EtherType.IPv6.Name);

        Assert.True(EtherType.IPv4.IsKnown);
        Assert.True(EtherType.IPv4.IsSpecified);
        Assert.True(EtherType.IPv4.IsEthernetII);
    }

    [Fact]
    public void Unspecified_IsNotSpecifiedAndNotKnown()
    {
        Assert.False(EtherType.Unspecified.IsSpecified);
        Assert.False(EtherType.Unspecified.IsKnown);
        Assert.Equal("Unspecified", EtherType.Unspecified.Name);
        Assert.Equal(EtherType.Unspecified, default(EtherType));
    }

    [Fact]
    public void UnimplementedValue_IsRepresentableWithoutThrowing_AndReadsAsUnknown()
    {
        var custom = new EtherType(0x88CC); // LLDP - not implemented in this project

        Assert.True(custom.IsSpecified);
        Assert.False(custom.IsKnown);
        Assert.Equal("Unknown", custom.Name);
        Assert.True(custom.IsEthernetII);
        Assert.Contains("0x88CC", custom.ToString());
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        Assert.Equal(new EtherType(0x0800), EtherType.IPv4);
        Assert.True(new EtherType(0x0800) == EtherType.IPv4);
        Assert.NotEqual(EtherType.IPv4, EtherType.IPv6);
    }

    [Fact]
    public void IsEthernetII_IsFalseBelow0x0600()
    {
        Assert.False(new EtherType(0x05DC).IsEthernetII);
        Assert.True(new EtherType(0x0600).IsEthernetII);
    }
}
