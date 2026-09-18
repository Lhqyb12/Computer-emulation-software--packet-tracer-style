using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class Ipv6InterfaceConfigurationTests
{
    [Fact]
    public void Create_ExposesAddressPrefixAndDerivedNetwork()
    {
        var config = Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8:1::10"), 64);

        Assert.Equal(IPv6Address.Parse("2001:db8:1::10"), config.Address);
        Assert.Equal(64, config.PrefixLength);
        Assert.True(config.IsPrimary);
        Assert.Equal("2001:db8:1::10/64", config.Cidr);
        Assert.Equal(IPv6Network.Parse("2001:db8:1::/64"), config.Network);
    }

    [Fact]
    public void Create_AllowsLinkLocalAndUniqueLocalAddresses()
    {
        Assert.Equal(IPv6AddressCategory.LinkLocal, Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("fe80::1"), 64).Address.Category);
        Assert.Equal(IPv6AddressCategory.UniqueLocal, Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("fd00::1"), 64).Address.Category);
    }

    [Theory]
    [InlineData("::")]
    [InlineData("::1")]
    [InlineData("ff02::1")]
    public void Create_RejectsAddressesThatCannotBeInterfaceAddresses(string address)
    {
        Assert.Throws<DomainException>(() => Ipv6InterfaceConfiguration.Create(IPv6Address.Parse(address), 64));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(129)]
    public void Create_RejectsPrefixOutOfRange(int prefix)
    {
        Assert.Throws<DomainException>(() => Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8::1"), prefix));
    }

    [Fact]
    public void AsSecondary_AndAsPrimary_FlipTheFlag_WithValueEquality()
    {
        var primary = Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8::1"), 64);
        var secondary = primary.AsSecondary();

        Assert.False(secondary.IsPrimary);
        Assert.NotEqual(primary, secondary);
        Assert.Equal(primary, secondary.AsPrimary());
        Assert.Same(primary, primary.AsPrimary());
    }

    [Fact]
    public void Equality_IsAddressPrefixAndPrimaryFlag()
    {
        var a = Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8::1"), 64);
        var b = Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:0db8::1"), 64);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, Ipv6InterfaceConfiguration.Create(IPv6Address.Parse("2001:db8::1"), 48));
    }
}
