using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

/// <summary>
/// Phase 19: a <see cref="NetworkInterface"/> carries an IPv6 configuration (zero, one or several
/// addresses - a link-local plus global/unique-local addresses is the norm), independent of its
/// MAC / Ethernet capability and of any IPv4 configuration.
/// </summary>
public class NetworkInterfaceIPv6Tests
{
    private static NetworkInterface NewEthernetInterface() =>
        new Pc("PC1").AddInterface("Ethernet0", InterfaceType.Ethernet);

    [Fact]
    public void NewInterface_HasNoIPv6Configuration()
    {
        var iface = NewEthernetInterface();

        Assert.False(iface.HasIPv6Configuration);
        Assert.Empty(iface.IPv6Configurations);
        Assert.Null(iface.IPv6Address);
        Assert.Null(iface.PrimaryIPv6Configuration);
    }

    [Fact]
    public void AddIPv6Configuration_FirstAddressBecomesPrimary()
    {
        var iface = NewEthernetInterface();

        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::1"), 64));

        Assert.True(iface.HasIPv6Configuration);
        Assert.Equal(IPv6Address.Parse("2001:db8:1::1"), iface.IPv6Address);
        Assert.True(iface.PrimaryIPv6Configuration!.IsPrimary);
        Assert.Equal("2001:db8:1::1/64", iface.PrimaryIPv6Configuration!.Cidr);
    }

    [Fact]
    public void AddIPv6Configuration_SupportsMultipleAddresses_KeepingASinglePrimary()
    {
        var iface = NewEthernetInterface();

        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("fe80::1"), 64));
        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::1"), 64, isPrimary: true));

        Assert.Equal(2, iface.IPv6Configurations.Count);
        Assert.Equal(IPv6Address.Parse("fe80::1"), iface.IPv6Address);
        Assert.Single(iface.IPv6Configurations, c => c.IsPrimary);
        Assert.Contains(iface.IPv6Configurations, c => c.Address == IPv6Address.Parse("2001:db8:1::1") && !c.IsPrimary);
    }

    [Fact]
    public void AddIPv6Configuration_RejectsADuplicateAddress()
    {
        var iface = NewEthernetInterface();
        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::1"), 64));

        Assert.Throws<DomainException>(() =>
            iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:0db8:1::1"), 48)));
    }

    [Fact]
    public void SetPrimaryIPv6Configuration_ReplacesEverythingWithASinglePrimary()
    {
        var iface = NewEthernetInterface();
        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("fe80::1"), 64));
        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::1"), 64));

        iface.SetPrimaryIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:2::2"), 64));

        var only = Assert.Single(iface.IPv6Configurations);
        Assert.Equal(IPv6Address.Parse("2001:db8:2::2"), only.Address);
        Assert.True(only.IsPrimary);
    }

    [Fact]
    public void RemoveIPv6Configuration_PromotesTheNextAddressWhenThePrimaryIsRemoved()
    {
        var iface = NewEthernetInterface();
        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("fe80::1"), 64));
        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::1"), 64));

        Assert.True(iface.RemoveIPv6Configuration(IPv6Address.Parse("fe80::1")));

        var only = Assert.Single(iface.IPv6Configurations);
        Assert.Equal(IPv6Address.Parse("2001:db8:1::1"), only.Address);
        Assert.True(only.IsPrimary);
        Assert.False(iface.RemoveIPv6Configuration(IPv6Address.Parse("2001:db8:9::9")));
    }

    [Fact]
    public void ClearIPv6Configuration_RemovesEverything()
    {
        var iface = NewEthernetInterface();
        iface.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::1"), 64));

        iface.ClearIPv6Configuration();

        Assert.False(iface.HasIPv6Configuration);
        Assert.Null(iface.IPv6Address);
    }

    [Fact]
    public void IPv6_CanBeConfiguredOnANonEthernetInterface()
    {
        var serial = new Router("R1").AddInterface("Serial0/0", InterfaceType.Serial);

        serial.AddIPv6Configuration(Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8::1"), 127));

        Assert.Equal(IPv6Address.Parse("2001:db8::1"), serial.IPv6Address);
        Assert.Null(serial.MacAddress);
    }
}
