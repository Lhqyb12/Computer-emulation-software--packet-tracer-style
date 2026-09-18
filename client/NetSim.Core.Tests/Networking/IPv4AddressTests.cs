using NetSim.Core.Common.Exceptions;
using NetSim.Core.Networking;

namespace NetSim.Core.Tests.Networking;

public class IPv4AddressTests
{
    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("127.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("192.168.0.10")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.5.20")]
    [InlineData("255.255.255.255")]
    public void Parse_AcceptsValidDottedDecimal_AndRoundTrips(string input)
    {
        var address = IPv4Address.Parse(input);

        Assert.Equal(input, address.ToString());
    }

    [Theory]
    [InlineData("256.1.1.1")]
    [InlineData("192.168.1")]
    [InlineData("192.168.1.1.1")]
    [InlineData("abc.def.ghi.jkl")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("-1.0.0.0")]
    [InlineData("192.168.1.-4")]
    [InlineData("192.168. 1.1")]
    [InlineData("192.168.1.")]
    [InlineData("1.2.3.4 ")]
    [InlineData("0x7f.0.0.1")]
    public void Parse_RejectsInvalidInput(string? input)
    {
        Assert.False(IPv4Address.TryParse(input, out _));
        Assert.Throws<DomainException>(() => IPv4Address.Parse(input));
    }

    [Fact]
    public void Equality_IsValueBased()
    {
        var a = IPv4Address.Parse("192.168.1.10");
        var b = IPv4Address.Parse("192.168.1.10");
        var different = IPv4Address.Parse("192.168.1.11");

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a == different);
        Assert.NotEqual(a, different);
    }

    [Fact]
    public void GetHashCode_MatchesForEqualAddresses()
    {
        Assert.Equal(
            IPv4Address.Parse("10.20.30.40").GetHashCode(),
            IPv4Address.Parse("10.20.30.40").GetHashCode());
    }

    [Fact]
    public void GetBytes_ReturnsFourOctetsMostSignificantFirst()
    {
        Assert.Equal(new byte[] { 192, 168, 1, 10 }, IPv4Address.Parse("192.168.1.10").GetBytes());
    }

    [Fact]
    public void FromBytes_RebuildsTheAddress_AndRejectsWrongLength()
    {
        Assert.Equal(IPv4Address.Parse("192.168.1.10"), IPv4Address.FromBytes([192, 168, 1, 10]));
        Assert.Throws<DomainException>(() => IPv4Address.FromBytes([1, 2, 3]));
    }

    [Fact]
    public void Value_IsHostOrder_WithFirstOctetMostSignificant()
    {
        Assert.Equal(0xC0A8_010Au, IPv4Address.Parse("192.168.1.10").Value);
        Assert.Equal(IPv4Address.Parse("192.168.1.10"), new IPv4Address(0xC0A8_010Au));
    }

    [Fact]
    public void BoundaryValues_AndConstants_Match()
    {
        Assert.Equal(IPv4Address.Parse("0.0.0.0"), IPv4Address.Any);
        Assert.Equal(IPv4Address.Parse("0.0.0.0"), default(IPv4Address));
        Assert.Equal(IPv4Address.Parse("127.0.0.1"), IPv4Address.Loopback);
        Assert.Equal(IPv4Address.Parse("255.255.255.255"), IPv4Address.Broadcast);
    }

    [Fact]
    public void Comparison_OrdersNumerically()
    {
        var low = IPv4Address.Parse("10.0.0.1");
        var high = IPv4Address.Parse("10.0.1.0");

        Assert.True(low < high);
        Assert.True(high > low);
        Assert.True(low <= IPv4Address.Parse("10.0.0.1"));
        Assert.True(low.CompareTo(high) < 0);
    }

    [Theory]
    [InlineData("0.0.0.0", IPv4AddressCategory.Unspecified)]
    [InlineData("127.0.0.1", IPv4AddressCategory.Loopback)]
    [InlineData("127.255.255.254", IPv4AddressCategory.Loopback)]
    [InlineData("169.254.10.5", IPv4AddressCategory.LinkLocal)]
    [InlineData("10.1.2.3", IPv4AddressCategory.Private)]
    [InlineData("172.16.0.1", IPv4AddressCategory.Private)]
    [InlineData("172.31.255.255", IPv4AddressCategory.Private)]
    [InlineData("192.168.100.100", IPv4AddressCategory.Private)]
    [InlineData("224.0.0.1", IPv4AddressCategory.Multicast)]
    [InlineData("239.255.255.255", IPv4AddressCategory.Multicast)]
    [InlineData("255.255.255.255", IPv4AddressCategory.LimitedBroadcast)]
    [InlineData("8.8.8.8", IPv4AddressCategory.Public)]
    [InlineData("172.32.0.1", IPv4AddressCategory.Public)]
    [InlineData("172.15.255.255", IPv4AddressCategory.Public)]
    public void Category_ClassifiesSpecialRanges(string input, IPv4AddressCategory expected)
    {
        Assert.Equal(expected, IPv4Address.Parse(input).Category);
    }

    [Fact]
    public void ClassificationFlags_AreConsistentWithCategory()
    {
        Assert.True(IPv4Address.Parse("10.0.0.1").IsPrivate);
        Assert.False(IPv4Address.Parse("10.0.0.1").IsPublic);
        Assert.True(IPv4Address.Parse("127.0.0.1").IsLoopback);
        Assert.True(IPv4Address.Any.IsUnspecified);
        Assert.True(IPv4Address.Broadcast.IsLimitedBroadcast);
        Assert.True(IPv4Address.Parse("8.8.8.8").IsPublic);
        Assert.True(IPv4Address.Parse("224.0.0.5").IsMulticast);
    }
}
