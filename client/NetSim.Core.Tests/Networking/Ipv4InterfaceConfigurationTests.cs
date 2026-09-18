using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class Ipv4InterfaceConfigurationTests
{
    [Fact]
    public void Create_ExposesAddressPrefixMaskAndDerivedNetwork()
    {
        var config = Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24);

        Assert.Equal(IPv4Address.Parse("192.168.1.10"), config.Address);
        Assert.Equal(24, config.PrefixLength);
        Assert.Equal(SubnetMask.Parse("255.255.255.0"), config.SubnetMask);
        Assert.True(config.IsPrimary);
        Assert.Equal("192.168.1.10/24", config.Cidr);
        Assert.Equal(IPv4Network.Parse("192.168.1.0/24"), config.Network);
    }

    [Fact]
    public void Create_FromMask_IsEquivalentToPrefix()
    {
        Assert.Equal(
            Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.1"), 8),
            Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("10.0.0.1"), SubnetMask.Parse("255.0.0.0")));
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    public void Create_RejectsAddressesThatCannotBeHostAddresses(string address)
    {
        Assert.Throws<DomainException>(() => Ipv4InterfaceConfiguration.Create(IPv4Address.Parse(address), 24));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(33)]
    public void Create_RejectsPrefixOutOfRange(int prefix)
    {
        Assert.Throws<DomainException>(() => Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), prefix));
    }

    [Fact]
    public void AsSecondary_AndAsPrimary_FlipTheFlag_WithValueEquality()
    {
        var primary = Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24);
        var secondary = primary.AsSecondary();

        Assert.False(secondary.IsPrimary);
        Assert.NotEqual(primary, secondary);
        Assert.Equal(primary, secondary.AsPrimary());
        Assert.Same(primary, primary.AsPrimary());
    }

    [Fact]
    public void Equality_IsAddressPrefixAndPrimaryFlag()
    {
        var a = Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24);
        var b = Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 24);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, Ipv4InterfaceConfiguration.Create(IPv4Address.Parse("192.168.1.10"), 25));
    }
}
