using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class IPv4NetworkTests
{
    [Theory]
    // address in the block, prefix, expected network, expected broadcast, first host, last host, usable count
    [InlineData("192.168.1.25", 24, "192.168.1.0", "192.168.1.255", "192.168.1.1", "192.168.1.254", 254)]
    [InlineData("192.168.1.130", 26, "192.168.1.128", "192.168.1.191", "192.168.1.129", "192.168.1.190", 62)]
    [InlineData("10.0.0.1", 8, "10.0.0.0", "10.255.255.255", "10.0.0.1", "10.255.255.254", 16777214)]
    [InlineData("172.16.5.20", 16, "172.16.0.0", "172.16.255.255", "172.16.0.1", "172.16.255.254", 65534)]
    [InlineData("192.168.1.200", 25, "192.168.1.128", "192.168.1.255", "192.168.1.129", "192.168.1.254", 126)]
    [InlineData("192.168.1.10", 27, "192.168.1.0", "192.168.1.31", "192.168.1.1", "192.168.1.30", 30)]
    [InlineData("192.168.1.50", 28, "192.168.1.48", "192.168.1.63", "192.168.1.49", "192.168.1.62", 14)]
    [InlineData("192.168.1.100", 29, "192.168.1.96", "192.168.1.103", "192.168.1.97", "192.168.1.102", 6)]
    [InlineData("192.168.1.5", 30, "192.168.1.4", "192.168.1.7", "192.168.1.5", "192.168.1.6", 2)]
    public void Create_ComputesNetworkBroadcastAndHostRange(
        string address, int prefix, string network, string broadcast, string firstHost, string lastHost, long usableCount)
    {
        var subnet = IPv4Network.Create(IPv4Address.Parse(address), prefix);

        Assert.Equal(IPv4Address.Parse(network), subnet.NetworkAddress);
        Assert.Equal(prefix, subnet.PrefixLength);
        Assert.Equal(SubnetMask.FromPrefixLength(prefix), subnet.SubnetMask);
        Assert.Equal(IPv4Address.Parse(broadcast), subnet.BroadcastAddress);
        Assert.True(subnet.HasBroadcastAddress);
        Assert.Equal(IPv4Address.Parse(firstHost), subnet.FirstUsableHost);
        Assert.Equal(IPv4Address.Parse(lastHost), subnet.LastUsableHost);
        Assert.Equal(usableCount, subnet.UsableHostCount);
    }

    [Fact]
    public void Create_NormalisesAnyAddressInTheBlockToTheNetworkAddress()
    {
        Assert.Equal(
            IPv4Network.Parse("192.168.1.0/24"),
            IPv4Network.Create(IPv4Address.Parse("192.168.1.99"), 24));
    }

    [Fact]
    public void Slash31_HasTwoUsableHosts_AndNoBroadcast()
    {
        var subnet = IPv4Network.Create(IPv4Address.Parse("10.0.0.4"), 31);

        Assert.False(subnet.HasBroadcastAddress);
        Assert.Null(subnet.BroadcastAddress);
        Assert.Equal(2, subnet.UsableHostCount);
        Assert.Equal(IPv4Address.Parse("10.0.0.4"), subnet.FirstUsableHost);
        Assert.Equal(IPv4Address.Parse("10.0.0.5"), subnet.LastUsableHost);
        Assert.Equal(2, subnet.TotalAddressCount);
    }

    [Fact]
    public void Slash32_IsASingleHost_WithNoBroadcast()
    {
        var subnet = IPv4Network.Create(IPv4Address.Parse("10.0.0.9"), 32);

        Assert.False(subnet.HasBroadcastAddress);
        Assert.Null(subnet.BroadcastAddress);
        Assert.Equal(1, subnet.UsableHostCount);
        Assert.Equal(1, subnet.TotalAddressCount);
        Assert.Equal(IPv4Address.Parse("10.0.0.9"), subnet.FirstUsableHost);
        Assert.Equal(IPv4Address.Parse("10.0.0.9"), subnet.LastUsableHost);
        Assert.Equal(IPv4Address.Parse("10.0.0.9"), subnet.NetworkAddress);
    }

    [Fact]
    public void Slash0_CoversTheEntireAddressSpace()
    {
        var subnet = IPv4Network.Create(IPv4Address.Parse("8.8.8.8"), 0);

        Assert.Equal(IPv4Address.Any, subnet.NetworkAddress);
        Assert.Equal(4294967296L, subnet.TotalAddressCount);
        Assert.Equal(4294967294L, subnet.UsableHostCount);
        Assert.True(subnet.Contains(IPv4Address.Parse("1.2.3.4")));
    }

    [Theory]
    [InlineData("192.168.1.0/24", "192.168.1.50", true)]
    [InlineData("192.168.1.0/24", "192.168.1.255", true)]
    [InlineData("192.168.1.0/24", "192.168.2.50", false)]
    [InlineData("192.168.1.128/26", "192.168.1.130", true)]
    [InlineData("192.168.1.128/26", "192.168.1.127", false)]
    [InlineData("192.168.1.128/26", "192.168.1.192", false)]
    [InlineData("10.0.0.0/8", "10.255.255.255", true)]
    [InlineData("10.0.0.0/8", "11.0.0.0", false)]
    public void Contains_TestsAddressMembership(string cidr, string address, bool expected)
    {
        Assert.Equal(expected, IPv4Network.Parse(cidr).Contains(IPv4Address.Parse(address)));
    }

    [Fact]
    public void Contains_TestsSubnetMembership_ForLongestPrefixMatchLater()
    {
        var supernet = IPv4Network.Parse("10.0.0.0/8");

        Assert.True(supernet.Contains(IPv4Network.Parse("10.1.2.0/24")));
        Assert.True(supernet.Contains(IPv4Network.Parse("10.0.0.0/8")));
        Assert.False(supernet.Contains(IPv4Network.Parse("11.0.0.0/24")));
        Assert.False(supernet.Contains(IPv4Network.Parse("0.0.0.0/0")));
    }

    [Theory]
    [InlineData("255.255.255.0")]
    [InlineData("192.168.1.0/33")]
    [InlineData("192.168.1.0/-1")]
    [InlineData("192.168.1.0/abc")]
    [InlineData("999.1.1.1/24")]
    [InlineData("192.168.1.0/")]
    [InlineData("/24")]
    [InlineData(null)]
    public void Parse_RejectsMalformedCidr(string? cidr)
    {
        Assert.False(IPv4Network.TryParse(cidr, out _));
        Assert.Throws<DomainException>(() => IPv4Network.Parse(cidr));
    }

    [Fact]
    public void Equality_IsNetworkAddressPlusPrefix()
    {
        Assert.Equal(IPv4Network.Parse("192.168.1.0/24"), IPv4Network.Create(IPv4Address.Parse("192.168.1.77"), 24));
        Assert.NotEqual(IPv4Network.Parse("192.168.1.0/24"), IPv4Network.Parse("192.168.1.0/25"));
        Assert.Equal(
            IPv4Network.Parse("192.168.1.0/24").GetHashCode(),
            IPv4Network.Parse("192.168.1.0/24").GetHashCode());
    }

    [Fact]
    public void ToString_IsCidrNotation()
    {
        Assert.Equal("192.168.1.128/26", IPv4Network.Create(IPv4Address.Parse("192.168.1.130"), 26).ToString());
    }
}
