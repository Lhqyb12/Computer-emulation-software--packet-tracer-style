using NetSim.Core.Arp;

namespace NetSim.Core.Tests.Arp;

public class ArpHardwareTypeTests
{
    [Fact]
    public void Ethernet_IsHardwareType1()
    {
        Assert.Equal(1, ArpHardwareType.Ethernet.Value);
        Assert.True(ArpHardwareType.Ethernet.IsEthernet);
        Assert.Equal("Ethernet", ArpHardwareType.Ethernet.Name);
    }

    [Fact]
    public void UnknownValue_IsRepresentable_AndNotEthernet()
    {
        var other = new ArpHardwareType(6);

        Assert.False(other.IsEthernet);
        Assert.Equal("Unknown", other.Name);
        Assert.Equal(6, other.Value);
    }

    [Fact]
    public void ValueEquality()
    {
        Assert.Equal(new ArpHardwareType(1), ArpHardwareType.Ethernet);
        Assert.NotEqual(new ArpHardwareType(1), new ArpHardwareType(2));
    }
}
