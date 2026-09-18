using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

/// <summary>
/// Phase 19 brief section 44 / 53: IPv4 and IPv6 are two different Layer 3 protocols and both must
/// coexist on the same interface without one breaking the other.
/// </summary>
public class NetworkInterfaceDualStackTests
{
    [Fact]
    public void AnInterfaceCanCarryBothIPv4AndIPv6_Independently()
    {
        var iface = new Router("R0").AddInterface("GigabitEthernet0/0", InterfaceType.Ethernet);

        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.1"), 24));
        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("fe80::1"), 64));
        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::1"), 64));

        Assert.Equal(IPv4Address.Parse("192.168.1.1"), iface.IPv4Address);
        Assert.Equal("192.168.1.1/24", iface.PrimaryIPv4Configuration!.Cidr);

        Assert.Equal(IPv6Address.Parse("fe80::1"), iface.IPv6Address);
        Assert.Equal(2, iface.IPv6Configurations.Count);
        Assert.NotNull(iface.MacAddress);
    }

    [Fact]
    public void ClearingOneProtocol_LeavesTheOtherUntouched()
    {
        var iface = new Router("R0").AddInterface("GigabitEthernet0/0", InterfaceType.Ethernet);
        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.1"), 24));
        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::1"), 64));

        iface.ClearIPv6Configuration();

        Assert.False(iface.HasIPv6Configuration);
        Assert.True(iface.HasIPv4Configuration);
        Assert.Equal(IPv4Address.Parse("192.168.1.1"), iface.IPv4Address);

        iface.ClearIPv4Configuration();
        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::1"), 64));

        Assert.False(iface.HasIPv4Configuration);
        Assert.True(iface.HasIPv6Configuration);
    }
}
