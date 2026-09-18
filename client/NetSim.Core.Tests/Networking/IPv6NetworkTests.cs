using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class IPv6NetworkTests
{
    [Theory]
    [InlineData("2001:db8:1234:5678::1", 48, "2001:db8:1234::")]
    [InlineData("2001:db8:1234:5678::1", 64, "2001:db8:1234:5678::")]
    [InlineData("2001:db8:1234:5678::1", 32, "2001:db8::")]
    [InlineData("2001:db8:1234:5678:9abc::1", 0, "::")]
    [InlineData("2001:db8:1234:5678:9abc::1", 128, "2001:db8:1234:5678:9abc::1")]
    [InlineData("2001:db8::abcd", 127, "2001:db8::abcc")]
    [InlineData("2001:db8::abcd", 96, "2001:db8::")]
    [InlineData("2001:db8:0:0:1234::1", 1, "::")]
    public void Create_NormalisesTheAddressToThePrefix(string address, int prefix, string expectedNetwork)
    {
        var network = IPv6Network.Create(IPv6Address.Parse(address), prefix);

        Assert.Equal(IPv6Address.Parse(expectedNetwork), network.NetworkAddress);
        Assert.Equal(prefix, network.PrefixLength);
    }

    [Fact]
    public void Contains_Address_ChecksPrefixMembership()
    {
        var network = IPv6Network.Parse("2001:db8:1234::/48")!;

        Assert.True(network.Contains(IPv6Address.Parse("2001:db8:1234:1::10")));
        Assert.True(network.Contains(IPv6Address.Parse("2001:db8:1234:5678::1")));
        Assert.False(network.Contains(IPv6Address.Parse("2001:db8:5678::10")));
        Assert.False(network.Contains(IPv6Address.Parse("2001:db8:9999::1")));
    }

    [Fact]
    public void Contains_Network_IsTrueOnlyForEqualOrMoreSpecificPrefixes()
    {
        var slash32 = IPv6Network.Parse("2001:db8::/32")!;

        Assert.True(slash32.Contains(IPv6Network.Parse("2001:db8:1234::/48")!));
        Assert.True(slash32.Contains(IPv6Network.Parse("2001:db8::/32")!));
        Assert.False(slash32.Contains(IPv6Network.Parse("2001:db8::/16")!));
        Assert.False(slash32.Contains(IPv6Network.Parse("2001:dead::/48")!));
    }

    [Fact]
    public void FirstAndLastAddress_SpanTheBlock()
    {
        var network = IPv6Network.Create(IPv6Address.Parse("2001:db8:1234:5678::1"), 64);

        Assert.Equal(IPv6Address.Parse("2001:db8:1234:5678::"), network.FirstAddress);
        Assert.Equal(IPv6Address.Parse("2001:db8:1234:5678:ffff:ffff:ffff:ffff"), network.LastAddress);
    }

    [Fact]
    public void Slash128_IsASingleAddress()
    {
        var network = IPv6Network.Create(IPv6Address.Parse("2001:db8::1"), 128);

        Assert.Equal(network.FirstAddress, network.LastAddress);
        Assert.True(network.Contains(IPv6Address.Parse("2001:db8::1")));
        Assert.False(network.Contains(IPv6Address.Parse("2001:db8::2")));
    }

    [Fact]
    public void Slash0_ContainsEverything()
    {
        var network = IPv6Network.Create(IPv6Address.Parse("2001:db8::1"), 0);

        Assert.Equal(IPv6Address.Unspecified, network.FirstAddress);
        Assert.Equal(IPv6Address.Parse("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff"), network.LastAddress);
        Assert.True(network.Contains(IPv6Address.Parse("fe80::1")));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(129)]
    public void Create_RejectsPrefixOutOfRange(int prefix)
    {
        Assert.Throws<DomainException>(() => IPv6Network.Create(IPv6Address.Parse("2001:db8::1"), prefix));
    }

    [Theory]
    [InlineData("2001:db8::/48", "2001:db8::/48")]
    [InlineData("2001:db8:1234:5678::1/64", "2001:db8:1234:5678::/64")]
    [InlineData("2001:0db8::/32", "2001:db8::/32")]
    public void Parse_RoundTripsCanonicalCidr(string input, string expected)
    {
        Assert.Equal(expected, IPv6Network.Parse(input)!.ToString());
    }

    [Theory]
    [InlineData("2001:db8::")]
    [InlineData("2001:db8::/")]
    [InlineData("/64")]
    [InlineData("2001:db8::/129")]
    [InlineData("2001:db8:::1/64")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_RejectsMalformedCidr(string? input)
    {
        Assert.False(IPv6Network.TryParse(input, out _));
        Assert.Throws<DomainException>(() => IPv6Network.Parse(input));
    }

    [Fact]
    public void Equality_IsNetworkAddressAndPrefixLength()
    {
        var a = IPv6Network.Parse("2001:db8:1234::/48")!;
        var b = IPv6Network.Create(IPv6Address.Parse("2001:db8:1234:5678::9"), 48);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, IPv6Network.Parse("2001:db8:1234::/64")!);
    }
}
