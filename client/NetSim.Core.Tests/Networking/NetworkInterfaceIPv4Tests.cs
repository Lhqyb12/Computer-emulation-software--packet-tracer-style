using NetSim.Core.Common.Exceptions;
using NetSim.Core.Devices;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

/// <summary>
/// Phase 18: a <see cref="NetworkInterface"/> carries an IPv4 configuration (zero, one or several
/// addresses), independent of its MAC / Ethernet capability.
/// </summary>
public class NetworkInterfaceIPv4Tests
{
    private static NetworkInterface NewEthernetInterface() =>
        new Pc("PC1").AddInterface("Ethernet0", InterfaceType.Ethernet);

    [Fact]
    public void NewInterface_HasNoIPv4Configuration()
    {
        var iface = NewEthernetInterface();

        Assert.False(iface.HasIPv4Configuration);
        Assert.Empty(iface.IPv4Configurations);
        Assert.Null(iface.IPv4Address);
        Assert.Null(iface.PrimaryIPv4Configuration);
    }

    [Fact]
    public void AddIPv4Configuration_FirstAddressBecomesPrimary()
    {
        var iface = NewEthernetInterface();

        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));

        Assert.True(iface.HasIPv4Configuration);
        Assert.Equal(IPv4Address.Parse("192.168.1.10"), iface.IPv4Address);
        Assert.True(iface.PrimaryIPv4Configuration!.IsPrimary);
        Assert.Equal("192.168.1.10/24", iface.PrimaryIPv4Configuration!.Cidr);
    }

    [Fact]
    public void AddIPv4Configuration_LaterAddressesAreStoredAsSecondary()
    {
        var iface = NewEthernetInterface();
        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));

        // Even if the caller passes IsPrimary: true, the interface keeps a single primary.
        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.1"), 8, isPrimary: true));

        Assert.Equal(2, iface.IPv4Configurations.Count);
        Assert.Equal(IPv4Address.Parse("192.168.1.10"), iface.IPv4Address);
        Assert.Single(iface.IPv4Configurations, c => c.IsPrimary);
        Assert.Contains(iface.IPv4Configurations, c => c.Address == IPv4Address.Parse("10.0.0.1") && !c.IsPrimary);
    }

    [Fact]
    public void AddIPv4Configuration_RejectsADuplicateAddress()
    {
        var iface = NewEthernetInterface();
        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));

        Assert.Throws<DomainException>(() =>
            iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 25)));
    }

    [Fact]
    public void SetPrimaryIPv4Configuration_ReplacesEverythingWithASinglePrimary()
    {
        var iface = NewEthernetInterface();
        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));
        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.1"), 8));

        iface.SetPrimaryIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("172.16.0.5"), 16));

        var only = Assert.Single(iface.IPv4Configurations);
        Assert.Equal(IPv4Address.Parse("172.16.0.5"), only.Address);
        Assert.True(only.IsPrimary);
    }

    [Fact]
    public void RemoveIPv4Configuration_PromotesTheNextAddressWhenThePrimaryIsRemoved()
    {
        var iface = NewEthernetInterface();
        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));
        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.1"), 8));

        Assert.True(iface.RemoveIPv4Configuration(IPv4Address.Parse("192.168.1.10")));

        var only = Assert.Single(iface.IPv4Configurations);
        Assert.Equal(IPv4Address.Parse("10.0.0.1"), only.Address);
        Assert.True(only.IsPrimary);
        Assert.False(iface.RemoveIPv4Configuration(IPv4Address.Parse("4.4.4.4")));
    }

    [Fact]
    public void ClearIPv4Configuration_RemovesEverything()
    {
        var iface = NewEthernetInterface();
        iface.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24));

        iface.ClearIPv4Configuration();

        Assert.False(iface.HasIPv4Configuration);
        Assert.Null(iface.IPv4Address);
    }

    [Fact]
    public void IPv4_CanBeConfiguredOnANonEthernetInterface()
    {
        var serial = new Router("R1").AddInterface("Serial0/0", InterfaceType.Serial);

        serial.AddIPv4Configuration(Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.1"), 30));

        Assert.Equal(IPv4Address.Parse("10.0.0.1"), serial.IPv4Address);
        Assert.Null(serial.MacAddress);
    }
}
